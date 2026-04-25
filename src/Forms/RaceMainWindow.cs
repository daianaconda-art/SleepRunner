using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SleepRunner.Automation;
using SleepRunner.Automation.Race;
using SleepRunner.Automation.Race.Policy.Training;
using SleepRunner.Forms.Controls;
using SleepRunner.Forms.TrainingRules;
using SleepRunner.Utils;

namespace SleepRunner.Forms;

/// <summary>
/// 跑马自动化前端：紧凑垂直悬浮面板
///
/// 设计目标：
///   - 常驻在游戏窗口旁边，宽度固定 340，不抢视线
///   - 无系统标题栏，自绘 titlebar（应用名 + Pin / Min / Close 三个图标按钮）
///   - 内容自上而下分四块：状态卡片 → 主操作 → 配置卡片 → 极简 footer
///   - 设置（失败率阈值 / 等待倍率 / 基调 / 力量阈值 / 窗口位置 / 置顶）持久化到 UserSettings
///   - 不内嵌日志框；查问题去看 assets/logs/latest.log
/// </summary>
public sealed class RaceMainWindow : Form
{
    // ---------- 常量 ----------
    private const int DefaultPanelWidth = 408;
    private const int MinPanelWidth = 392;
    private const int MaxPanelWidth = 760;
    private const int PadX = 14;
    private const int TitleBarHeight = 46;
    private const int GapAfterTitle = 14;
    private const int GapAfterHero = 18;
    private const int GapAfterSection = 8;
    private const int GapAfterConfig = 14;
    private const int FooterHeight = 28;
    private const int BottomPad = 14;
    private const int HeroCardHeight = 102;
    private const int HeroCardTopPad = 12;
    private const int HeroCardSidePad = 16;
    private const int HeroActionsMinWidth = 188;
    private const int HeroActionsMaxWidth = 280;
    private const double HeroActionsWidthRatio = 0.48;
    private const int HeroActionsTopOffset = 12;
    private const int HeroStatusToActionsGap = 12;
    private const int HeroStatusMinWidth = 120;
    private const int CornerRadius = 16;
    /// <summary>窗体边缘的缩放热区厚度</summary>
    private const int ResizeBorderThickness = 6;
    private const int ResizeCornerSize = 14;

    // 真正使用的内容高度（BuildLayout 算完即固化），用于决定窗口最小高度与垂直布局基线
    private int _contentHeight;
    private Rectangle _heroCardBounds;

    // ---------- 状态 ----------
    private readonly IRaceController _controller;
    private readonly UserSettings _settings;

    private RaceStatusIndicator _status = null!;
    private RaceActionButtons _actions = null!;
    private HeroHostPanel _heroHost = null!;
    private RaceConfigStrip _config = null!;
    private ProfilesStrip _profiles = null!;
    private TrainingRulesStrip _trainingRules = null!;
    private FilesStrip _files = null!;
    private SectionHeader _sectionTuning = null!;
    private SectionHeader _sectionProfiles = null!;
    private SectionHeader _sectionTrainingRules = null!;
    private SectionHeader _sectionFiles = null!;

    // 自绘标题栏控件
    private Panel _titleBar = null!;
    private IconButton _btnPin = null!;
    private IconButton _btnMin = null!;
    private IconButton _btnClose = null!;
    private Label _lblTitle = null!;
    private Label _lblFooterVersion = null!;

    // 可空：OnResize 在 ctor 早期就可能被调用（设置 ClientSize 时），
    // 那一刻这个 timer 还未创建；用 null 表达"尚未就绪"
    private System.Windows.Forms.Timer? _saveDebounce;
    private const int SaveDebounceMs = 600;

    public RaceMainWindow() : this(new RaceAutomationController()) { }

    /// <summary>测试 / 替身用：注入自定义 IRaceController 实现</summary>
    internal RaceMainWindow(IRaceController controller)
    {
        _controller = controller;
        _settings = UserSettings.Load();
        _settings.ApplyToRaceConfig();

        // ---------- 窗体基本属性 ----------
        Text = UiText.App.WindowTitle;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        BackColor = RaceTheme.Bg;
        ForeColor = RaceTheme.TextPrimary;
        Font = RaceTheme.BodyFont();
        DoubleBuffered = true;
        ShowInTaskbar = true;
        KeyPreview = true;

        ApplyInitialPositionAndTopMost();
        BuildLayout();

        _saveDebounce = new System.Windows.Forms.Timer { Interval = SaveDebounceMs };
        _saveDebounce.Tick += (_, _) => { _saveDebounce.Stop(); SaveSettingsFromUi(); };

        _controller.StateChanged += OnControllerStateChanged;
        _controller.ActivityChanged += OnActivityChanged;
        RaceConfig.Changed += ScheduleSave;
        Move += (_, _) => ScheduleSave();
        FormClosing += OnFormClosing;

        ApplyState(RaceState.Idle);
    }

    // ---------- 启用窗体阴影 ----------
    /// <summary>
    /// 给无边框窗体追加 CS_DROPSHADOW，避免边缘"贴在屏幕上"的廉价感
    /// </summary>
    protected override CreateParams CreateParams
    {
        get
        {
            const int CS_DROPSHADOW = 0x00020000;
            var cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }

    // ---------- 自绘整窗背景 ----------
    /// <summary>
    /// 只在句柄创建 / 尺寸变化时刷新窗体 Region，避免 OnPaint 中反复分配 GDI 对象
    /// （拖拽时 paint 触发频次极高，每帧 new Region 会迅速耗尽 GDI 句柄并报 0x80004005）
    /// </summary>
    private void UpdateClipRegion()
    {
        if (!IsHandleCreated || Width <= 0 || Height <= 0) return;
        var rect = new Rectangle(0, 0, Width, Height);
        var path = RaceTheme.BuildRoundedPath(rect, CornerRadius);
        var oldRegion = Region;
        Region = new Region(path);
        path.Dispose();
        oldRegion?.Dispose();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateClipRegion();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateClipRegion();
        // ctor 早期 ApplyInitialPositionAndTopMost 设置 ClientSize 也会触发本回调，
        // 那时子控件尚未创建；BuildLayout 末尾会显式调用 LayoutControls 兜底
        if (_status is not null)
            LayoutControls();
        // 用户拖拽改变窗口尺寸 → 节流落盘（debounce timer 在 ctor 末尾才建好）
        if (IsHandleCreated && WindowState == FormWindowState.Normal && _saveDebounce is not null)
            ScheduleSave();
    }

    // ---------- 拖拽缩放：自绘窗体没有原生 grip，靠 WM_NCHITTEST 把边缘伪装成系统边框 ----------
    private const int WM_NCHITTEST = 0x84;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;

    /// <summary>
    /// 在窗体边缘热区返回对应的 HT* 命中码，让系统接管缩放。
    /// 标题栏中段仍保留拖拽，但四角优先提供斜向缩放。
    /// </summary>
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            base.WndProc(ref m);

            int lp = m.LParam.ToInt32();
            short sx = (short)(lp & 0xFFFF);
            short sy = (short)((lp >> 16) & 0xFFFF);
            int hit = ResolveResizeHit(PointToClient(new Point(sx, sy)), ClientSize);
            if (hit != 0)
            {
                m.Result = (IntPtr)hit;
                return;
            }

            return;
        }
        base.WndProc(ref m);
    }

    private static int ResolveResizeHit(Point clientPos, Size clientSize)
    {
        int x = clientPos.X;
        int y = clientPos.Y;
        int w = clientSize.Width;
        int h = clientSize.Height;

        bool onLeft = x >= 0 && x < ResizeBorderThickness;
        bool onRight = x <= w && x > w - ResizeBorderThickness;
        bool onTop = y >= 0 && y < ResizeBorderThickness;
        bool onBottom = y <= h && y > h - ResizeBorderThickness;

        bool onTopLeft = x >= 0 && x < ResizeCornerSize && y >= 0 && y < ResizeCornerSize;
        bool onTopRight = x <= w && x > w - ResizeCornerSize && y >= 0 && y < ResizeCornerSize;
        bool onBottomLeft = x >= 0 && x < ResizeCornerSize && y <= h && y > h - ResizeCornerSize;
        bool onBottomRight = x <= w && x > w - ResizeCornerSize && y <= h && y > h - ResizeCornerSize;

        if (onTopLeft) return HTTOPLEFT;
        if (onTopRight) return HTTOPRIGHT;
        if (onBottomLeft) return HTBOTTOMLEFT;
        if (onBottomRight) return HTBOTTOMRIGHT;
        if (onLeft) return HTLEFT;
        if (onRight) return HTRIGHT;
        if (onTop) return HTTOP;
        if (onBottom) return HTBOTTOM;
        return 0;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var path = RaceTheme.BuildRoundedPath(new Rectangle(0, 0, Width, Height), CornerRadius);
        using var pen = new Pen(RaceTheme.BorderStrong, 1);
        g.DrawPath(pen, path);
    }

    // ---------- 初始位置 ----------
    private void ApplyInitialPositionAndTopMost()
    {
        var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        // 估算高度，真实高度在 BuildLayout 末尾会按内容收紧
        int estimatedHeight = TitleBarHeight + GapAfterTitle + HeroCardHeight + GapAfterHero + SectionHeader.RowHeight + GapAfterSection + RaceConfigStrip.CardHeight + GapAfterConfig
                              + SectionHeader.RowHeight + GapAfterSection + ProfilesStrip.CardHeight + GapAfterConfig
                              + SectionHeader.RowHeight + GapAfterSection + TrainingRulesStrip.CardHeight + GapAfterConfig
                              + SectionHeader.RowHeight + GapAfterSection + FilesStrip.CardHeight + GapAfterConfig
                              + FooterHeight + BottomPad;

        // 优先复用上次保存的窗口尺寸（用户拖拽过会写回），否则用默认值
        int initWidth = _settings.WindowWidth > 0
            ? Math.Clamp(_settings.WindowWidth, MinPanelWidth, MaxPanelWidth)
            : DefaultPanelWidth;
        int initHeight = _settings.WindowHeight > 0
            ? Math.Max(_settings.WindowHeight, estimatedHeight)
            : estimatedHeight;

        ClientSize = new Size(initWidth, initHeight);

        int x, y;
        if (_settings.WindowX >= 0 && _settings.WindowY >= 0)
        {
            x = _settings.WindowX;
            y = _settings.WindowY;
        }
        else
        {
            x = Math.Max(screen.Left, screen.Right - Width - 24);
            y = screen.Top + 24;
        }

        // 防越界：保证窗口至少 80px 在屏幕内
        int minX = screen.Left - Width + 80;
        int maxX = Math.Max(minX, screen.Right - 80);
        int minY = screen.Top;
        int maxY = Math.Max(minY, screen.Bottom - 60);
        x = Math.Clamp(x, minX, maxX);
        y = Math.Clamp(y, minY, maxY);

        Location = new Point(x, y);
        TopMost = _settings.TopMost;
    }

    // ---------- 布局 ----------
    /// <summary>
    /// 创建所有子控件（一次性），位置/尺寸交给 LayoutControls 按当前 ClientSize 计算。
    /// 拖拽缩放窗口时只调用 LayoutControls，不会重复 new 控件。
    /// </summary>
    private void BuildLayout()
    {
        BuildTitleBar();
        BuildHeroHost();

        // 状态卡片
        _status = new RaceStatusIndicator();
        _heroHost.Controls.Add(_status);

        // 主操作（Start / Stop）
        _actions = new RaceActionButtons();
        _actions.StartClicked += OnStartClicked;
        _actions.StopClicked += OnStopClicked;
        _heroHost.Controls.Add(_actions);

        // 分组标题 + 配置卡片
        _sectionTuning = new SectionHeader(UiText.Sections.Tuning);
        Controls.Add(_sectionTuning);

        _config = new RaceConfigStrip(_settings);
        _config.Changed += OnConfigChanged;
        Controls.Add(_config);

        // Profiles 分组：让用户挑选当前生效的 events/cards/trade JSON
        _sectionProfiles = new SectionHeader(UiText.Sections.Profiles);
        Controls.Add(_sectionProfiles);

        _profiles = new ProfilesStrip(_settings);
        _profiles.ProfilesChanged += OnProfilesChanged;
        Controls.Add(_profiles);

        // Training rules / config dirs 分组：先放训练规则，再放目录入口
        _sectionTrainingRules = new SectionHeader(UiText.Sections.TrainingRules);
        Controls.Add(_sectionTrainingRules);

        _trainingRules = new TrainingRulesStrip(_settings.TrainingProfile);
        _trainingRules.ProfileChanged += OnTrainingProfileChanged;
        _trainingRules.EditRequested += OnTrainingRuleEditRequested;
        _trainingRules.DuplicateRequested += OnTrainingRuleDuplicateRequested;
        Controls.Add(_trainingRules);

        _sectionFiles = new SectionHeader(UiText.Sections.ConfigDirs);
        Controls.Add(_sectionFiles);

        _files = new FilesStrip();
        Controls.Add(_files);

        // Footer：左侧版本，右侧拖拽提示
        BuildFooter();

        // 排布并按内容定下尺寸约束
        LayoutControls();
        // 注意：WinForms 的 MaximumSize 必须两个轴都 0 才表示"无限制"；
        // 单独把某一轴设为 0 会把窗口该维度强行钉到 0（之前出现过整个窗体被压成只剩标题栏）
        var screen = Screen.FromControl(this).WorkingArea;
        MinimumSize = new Size(MinPanelWidth, _contentHeight);
        MaximumSize = new Size(MaxPanelWidth, Math.Max(_contentHeight, screen.Height));
        // 第一次布局后把窗口收紧到至少能装下内容
        if (ClientSize.Height < _contentHeight)
            ClientSize = new Size(ClientSize.Width, _contentHeight);
    }

    private void BuildHeroHost()
    {
        _heroHost = new HeroHostPanel
        {
            BackColor = RaceTheme.Panel,
        };
        _heroHost.Paint += HeroHost_Paint;
        Controls.Add(_heroHost);
    }

    private void LayoutHeroChildren()
    {
        int heroClientW = _heroHost.ClientSize.Width;
        int actionsWidth = Math.Clamp((int)Math.Round(heroClientW * HeroActionsWidthRatio), HeroActionsMinWidth, HeroActionsMaxWidth);
        int heroTop = HeroCardTopPad;
        int actionsLeft = heroClientW - HeroCardSidePad - actionsWidth;
        int statusWidth = Math.Max(HeroStatusMinWidth, actionsLeft - HeroCardSidePad - HeroStatusToActionsGap);
        _status.SetBounds(HeroCardSidePad, heroTop, statusWidth, RaceStatusIndicator.CardHeight);
        _actions.SetBounds(actionsLeft, heroTop + HeroActionsTopOffset, actionsWidth, RaceActionButtons.RowHeight);
    }

    /// <summary>
    /// 按当前 ClientSize 重新排布所有子控件。
    /// 水平拖拽：所有卡片宽度跟随 innerWidth；标题栏右上角按钮贴右；footer 横向铺满。
    /// 垂直拖拽：内容固定从顶部依次堆叠，footer 钉在底部，剩余高度变成留白（不变形）。
    /// </summary>
    private void LayoutControls()
    {
        int clientW = Math.Max(MinPanelWidth, ClientSize.Width);
        int clientH = ClientSize.Height;
        int innerWidth = clientW - PadX * 2;

        // 标题栏
        if (_titleBar is not null)
        {
            _titleBar.Size = new Size(clientW, TitleBarHeight);
            LayoutTitleBarChildren(clientW);
        }

        int y = TitleBarHeight + GapAfterTitle;

        _heroCardBounds = new Rectangle(PadX, y, innerWidth, HeroCardHeight);
        if (_heroHost is not null)
        {
            _heroHost.SetBounds(_heroCardBounds.X, _heroCardBounds.Y, _heroCardBounds.Width, _heroCardBounds.Height);
            LayoutHeroChildren();
        }
        y = _heroCardBounds.Bottom + GapAfterHero;

        _sectionTuning.SetBounds(PadX + 2, y, innerWidth - 4, SectionHeader.RowHeight);
        y += SectionHeader.RowHeight + GapAfterSection;

        _config.SetBounds(PadX, y, innerWidth, RaceConfigStrip.CardHeight);
        y += _config.Height + GapAfterConfig;

        _sectionProfiles.SetBounds(PadX + 2, y, innerWidth - 4, SectionHeader.RowHeight);
        y += SectionHeader.RowHeight + GapAfterSection;

        _profiles.SetBounds(PadX, y, innerWidth, ProfilesStrip.CardHeight);
        y += _profiles.Height + GapAfterConfig;

        _sectionTrainingRules.SetBounds(PadX + 2, y, innerWidth - 4, SectionHeader.RowHeight);
        y += SectionHeader.RowHeight + GapAfterSection;

        _trainingRules.SetBounds(PadX, y, innerWidth, TrainingRulesStrip.CardHeight);
        y += _trainingRules.Height + GapAfterConfig;

        _sectionFiles.SetBounds(PadX + 2, y, innerWidth - 4, SectionHeader.RowHeight);
        y += SectionHeader.RowHeight + GapAfterSection;

        _files.SetBounds(PadX, y, innerWidth, FilesStrip.CardHeight);
        y += _files.Height + GapAfterConfig;

        // 内容总高度（footer 加上 bottom padding 之前）
        _contentHeight = y + FooterHeight + BottomPad;

        // Footer 始终钉在底部，留出 BottomPad
        if (_footer is not null)
        {
            int footerY = Math.Max(y, clientH - FooterHeight - BottomPad);
            _footer.SetBounds(PadX, footerY, innerWidth, FooterHeight);
            LayoutFooterChildren(innerWidth);
        }
    }

    private void BuildTitleBar()
    {
        _titleBar = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(ClientSize.Width, TitleBarHeight),
            BackColor = Color.Transparent,
        };
        _titleBar.Paint += TitleBar_Paint;
        _titleBar.MouseDown += TitleBar_MouseDown;
        Controls.Add(_titleBar);

        // 应用名（左侧），宽度交给 LayoutTitleBarChildren 计算
        _lblTitle = new Label
        {
            Text = UiText.App.WindowTitle,
            Font = RaceTheme.BoldFont(10.75F),
            ForeColor = RaceTheme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _lblTitle.MouseDown += TitleBar_MouseDown;
        _titleBar.Controls.Add(_lblTitle);

        // 右上角图标按钮组：Pin / Min / Close（位置交给 LayoutTitleBarChildren）
        _btnClose = new IconButton { Icon = IconButton.Glyph.Close, DangerHover = true };
        _btnClose.Click += (_, _) => Close();
        _titleBar.Controls.Add(_btnClose);

        _btnMin = new IconButton { Icon = IconButton.Glyph.Minimize };
        _btnMin.Click += (_, _) => WindowState = FormWindowState.Minimized;
        _titleBar.Controls.Add(_btnMin);

        _btnPin = new IconButton { Icon = IconButton.Glyph.Pin, Active = _settings.TopMost };
        _btnPin.Click += OnPinClicked;
        _titleBar.Controls.Add(_btnPin);
    }

    /// <summary>按当前标题栏宽度重排：Pin/Min/Close 贴右，title label 占据中间剩余空间</summary>
    private void LayoutTitleBarChildren(int barWidth)
    {
        const int btnW = 30;
        const int btnH = 28;
        int btnY = (TitleBarHeight - btnH) / 2;
        int rightX = barWidth - PadX;

        _btnClose.SetBounds(rightX - btnW, btnY, btnW, btnH);
        _btnMin.SetBounds(rightX - btnW * 2 - 4, btnY, btnW, btnH);
        _btnPin.SetBounds(rightX - btnW * 3 - 8, btnY, btnW, btnH);

        _lblTitle.SetBounds(PadX + 18, 0, _btnPin.Left - (PadX + 28), TitleBarHeight);
    }

    private Panel _footer = null!;
    private Label _lblFooterRight = null!;

    private void BuildFooter()
    {
        _footer = new Panel
        {
            BackColor = Color.Transparent,
        };
        Controls.Add(_footer);

        _lblFooterVersion = new Label
        {
            Text = UiText.App.FooterHint,
            Font = RaceTheme.SmallFont(),
            ForeColor = RaceTheme.TextDim,
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _footer.Controls.Add(_lblFooterVersion);

        _lblFooterRight = new Label
        {
            Text = UiText.App.Version,
            Font = RaceTheme.SmallFont(),
            ForeColor = RaceTheme.TextDim,
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
        };
        _footer.Controls.Add(_lblFooterRight);
    }

    /// <summary>按当前 footer 宽度重排：左侧提示填满 - 右端版本号固定 50px</summary>
    private void LayoutFooterChildren(int innerWidth)
    {
        const int rightW = 80;
        _lblFooterVersion.SetBounds(2, 0, Math.Max(0, innerWidth - rightW - 4), FooterHeight);
        _lblFooterRight.SetBounds(innerWidth - rightW, 0, rightW - 2, FooterHeight);
    }

    // ---------- 标题栏绘制 ----------
    /// <summary>
    /// 标题栏：左侧画一个迷你"应用图标"圆点 + 微妙底部分隔线
    /// </summary>
    private void TitleBar_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // 左侧应用图标：6px 圆点（主色）
        const int dotSize = 8;
        int dotX = PadX;
        int dotY = (_titleBar.Height - dotSize) / 2;
        var dotRect = new Rectangle(dotX, dotY, dotSize, dotSize);
        using (var halo = new SolidBrush(Color.FromArgb(70, RaceTheme.Accent)))
            g.FillEllipse(halo, Rectangle.Inflate(dotRect, 2, 2));
        using (var brush = new SolidBrush(RaceTheme.Accent))
            g.FillEllipse(brush, dotRect);

        // 标题栏底部 1px 分隔线
        using var pen = new Pen(RaceTheme.Border, 1);
        g.DrawLine(pen, 0, _titleBar.Height - 1, _titleBar.Width, _titleBar.Height - 1);
    }

    private void HeroHost_Paint(object? sender, PaintEventArgs e)
    {
        if (_heroHost.ClientSize.Width <= 0 || _heroHost.ClientSize.Height <= 0)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        RaceTheme.DrawRoundedPanel(e.Graphics, _heroHost.ClientRectangle, RaceTheme.Panel, RaceTheme.Border, 18);
    }

    private sealed class HeroHostPanel : Panel
    {
        public HeroHostPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.ResizeRedraw, true);
        }
    }

    // ---------- 拖拽 ----------
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;
    [DllImport("user32.dll")] private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();

    /// <summary>
    /// 在标题栏空白处按下时，把鼠标事件交给系统的窗体拖拽（标准 Win32 技巧）
    /// </summary>
    private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
    }

    // ---------- 键盘快捷键 ----------
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Esc 直接关闭窗口；游戏中需要快速收起面板
        if (keyData == Keys.Escape) { Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ---------- 控制器 ----------
    private void OnStartClicked()
    {
        _controller.Start();
    }

    private async void OnStopClicked()
    {
        _actions.DisableStop();
        try { await _controller.StopAsync(); }
        catch { /* 错误日志已在 controller 内打 */ }
    }

    private void OnConfigChanged()
    {
        ScheduleSave();
    }

    /// <summary>三个 profile 下拉变化 → 立即写回 settings 并触发节流落盘</summary>
    private void OnProfilesChanged(string events, string cards, string trade)
    {
        _settings.EventsProfile = events;
        _settings.CardsProfile = cards;
        _settings.TradeProfile = trade;
        ScheduleSave();
    }

    private void OnTrainingProfileChanged(string profile)
    {
        _settings.TrainingProfile = profile;
        TrainingRuleProfileManager.SetCurrentProfile(profile);
        ScheduleSave();
    }

    private void OnTrainingRuleEditRequested()
    {
        string currentProfile = _trainingRules.SelectedProfile;
        TrainingRuleProfile sourceProfile = TrainingRuleStore.CurrentProfile;
        if (!TrainingRuleEditorWindow.TryEditProfile(this, currentProfile, sourceProfile, out string savedProfile))
        {
            return;
        }

        ApplySavedTrainingProfile(savedProfile);
    }

    private void OnTrainingRuleDuplicateRequested()
    {
        string currentProfile = _trainingRules.SelectedProfile;
        TrainingRuleProfile sourceProfile = TrainingRuleStore.CurrentProfile;
        if (!TrainingRuleEditorWindow.TryDuplicateProfile(this, currentProfile, sourceProfile, out string savedProfile))
        {
            return;
        }

        ApplySavedTrainingProfile(savedProfile);
    }

    private void ApplySavedTrainingProfile(string profile)
    {
        _settings.TrainingProfile = profile;
        TrainingRuleProfileManager.SetCurrentProfile(profile);
        _trainingRules.RefreshProfiles(profile);
        ScheduleSave();
    }

    private void OnPinClicked(object? sender, EventArgs e)
    {
        bool newPin = !_btnPin.Active;
        _btnPin.Active = newPin;
        TopMost = newPin;
        ScheduleSave();
    }

    private void OnControllerStateChanged(RaceState newState)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
            BeginInvoke(new Action(() => ApplyState(newState)));
        else
            ApplyState(newState);
    }

    /// <summary>
    /// 自动化层活动描述变化（来自 ActivityReporter）
    /// 后台线程触发，必须切回 UI 线程更新状态卡
    /// </summary>
    private void OnActivityChanged(string text)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
            BeginInvoke(new Action(() => _status.SetActivity(text)));
        else
            _status.SetActivity(text);
    }

    private void ApplyState(RaceState state)
    {
        _status.ApplyState(state);
        _actions.ApplyState(state);
        // Idle / Stopped 时清掉活动描述，让默认副标题（"Ready to start" 等）回归
        if (state == RaceState.Idle || state == RaceState.Stopped)
            _status.SetActivity(null);
    }

    // ---------- 设置持久化 ----------
    /// <summary>调度一次延迟保存：连续触发只在最后一次后 600ms 落盘</summary>
    private void ScheduleSave()
    {
        if (IsDisposed || _saveDebounce is null) return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action(ScheduleSave));
            return;
        }
        _saveDebounce.Stop();
        _saveDebounce.Start();
    }

    /// <summary>把 UI / RaceConfig / 窗口几何信息同步回 UserSettings 并落盘</summary>
    private void SaveSettingsFromUi()
    {
        if (IsDisposed) return;

        _settings.WaitMultiplier = RaceConfig.WaitMultiplier;
        _settings.ClickSpeedMultiplier = RaceConfig.ClickSpeedMultiplier;
        _settings.TopMost = _btnPin.Active;

        // profile 名以 RaceProfileManager 当前真值为准（兼容程序启动时 settings 名比目录里多一份的情况）
        _settings.EventsProfile = Automation.Race.Policy.RaceProfileManager.CurrentEventsProfile;
        _settings.CardsProfile = Automation.Race.Policy.RaceProfileManager.CurrentCardsProfile;
        _settings.TradeProfile = Automation.Race.Policy.RaceProfileManager.CurrentTradeProfile;
        _settings.TrainingProfile = TrainingRuleProfileManager.CurrentProfile;

        if (WindowState == FormWindowState.Normal)
        {
            _settings.WindowX = Location.X;
            _settings.WindowY = Location.Y;
            _settings.WindowWidth = ClientSize.Width;
            _settings.WindowHeight = ClientSize.Height;
        }

        _settings.Save();
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _controller.StateChanged -= OnControllerStateChanged;
        _controller.ActivityChanged -= OnActivityChanged;
        RaceConfig.Changed -= ScheduleSave;
        _saveDebounce?.Stop();
        SaveSettingsFromUi();

        var state = _controller.State;
        if (state == RaceState.Running || state == RaceState.Paused || state == RaceState.Stopping)
        {
            e.Cancel = true;
            try { await _controller.StopAsync(); } catch { }
            _controller.Dispose();
            BeginInvoke(new Action(Close));
        }
        else
        {
            _controller.Dispose();
        }
    }
}
