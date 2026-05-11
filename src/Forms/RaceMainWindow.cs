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
    private const string IdleAppIconResourceName = "SleepRunner.AppIcon.ico";
    private const string RunningAppIconResourceName = "SleepRunner.RunningAppIcon.ico";
    private const int DefaultPanelWidth = 506;
    private const int MinPanelWidth = 500;
    private const int MaxPanelWidth = 860;
    private const int PadX = 14;
    private const int TitleBarHeight = 46;
    private const int GapAfterTitle = 14;
    private const int PageNavWidth = 86;
    private const int PageNavGap = 12;
    private const int PageNavButtonHeight = 42;
    private const int PageNavButtonGap = 8;
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
    private readonly Icon _idleIcon;
    private readonly Icon _runningIcon;

    private RaceStatusIndicator _status = null!;
    private RaceActionButtons _actions = null!;
    private HeroHostPanel _heroHost = null!;
    private RaceConfigStrip _config = null!;
    private ProfilesStrip _profiles = null!;
    private TrainingRulesStrip _trainingRules = null!;
    private FilesStrip _files = null!;
    private KeyLogStrip _keyLog = null!;
    private SectionHeader _sectionKeyLog = null!;
    private SectionHeader _sectionTuning = null!;
    private SectionHeader _sectionProfiles = null!;
    private SectionHeader _sectionTrainingRules = null!;
    private SectionHeader _sectionFiles = null!;
    private SidebarPanel _pageNav = null!;
    private Panel _automationPage = null!;
    private Panel _newPage = null!;
    private SidebarTabButton _btnAutomationPage = null!;
    private SidebarTabButton _btnNewPage = null!;
    private MainPage _activePage = MainPage.Automation;

    // 自绘标题栏控件
    private Panel _titleBar = null!;
    private IconButton _btnPin = null!;
    private IconButton _btnMin = null!;
    private IconButton _btnClose = null!;
    private Label _lblTitle = null!;
    private Label _lblFooterVersion = null!;
    private bool _automationHotkeyRegistered;
    private IntPtr _automationKeyboardHook;
    private LowLevelKeyboardProc? _automationKeyboardHookProc;
    private bool _automationHotkeyPressed;
    private bool _loggerSubscribed;

    // 可空：OnResize 在 ctor 早期就可能被调用（设置 ClientSize 时），
    // 那一刻这个 timer 还未创建；用 null 表达"尚未就绪"
    private System.Windows.Forms.Timer? _saveDebounce;
    private const int SaveDebounceMs = 600;

    private enum MainPage
    {
        Automation,
        NewPage,
    }

    public RaceMainWindow() : this(new RaceAutomationController()) { }

    private static Icon LoadAppIcon(string resourceName)
    {
        using Stream stream = typeof(RaceMainWindow).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded app icon resource '{resourceName}' was not found.");
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }

    /// <summary>测试 / 替身用：注入自定义 IRaceController 实现</summary>
    internal RaceMainWindow(IRaceController controller)
    {
        _controller = controller;
        _settings = UserSettings.Load();
        _settings.ApplyToRaceConfig();
        _idleIcon = LoadAppIcon(IdleAppIconResourceName);
        _runningIcon = LoadAppIcon(RunningAppIconResourceName);

        // ---------- 窗体基本属性 ----------
        Text = UiText.App.WindowTitle;
        Icon = _idleIcon;
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
        Logger.OnLog += OnLoggerLog;
        _loggerSubscribed = true;
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
        RegisterAutomationToggleHotkey();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterAutomationToggleHotkey();
        UninstallAutomationKeyboardHook();
        base.OnHandleDestroyed(e);
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
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == AutomationToggleHotkeyId)
        {
            ToggleAutomationFromHotkey();
            m.Result = IntPtr.Zero;
            return;
        }

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
        int estimatedHeight = TitleBarHeight + GapAfterTitle + HeroCardHeight + GapAfterHero
                              + SectionHeader.RowHeight + GapAfterSection + KeyLogStrip.CardHeight + GapAfterConfig
                              + SectionHeader.RowHeight + GapAfterSection + RaceConfigStrip.CardHeight + GapAfterConfig
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
        BuildPageShell();
        BuildHeroHost();

        // 状态卡片
        _status = new RaceStatusIndicator();
        _heroHost.Controls.Add(_status);

        // 主操作（Start / Stop）
        _actions = new RaceActionButtons();
        _actions.StartClicked += OnStartClicked;
        _actions.StopClicked += OnStopClicked;
        _heroHost.Controls.Add(_actions);

        _sectionKeyLog = new SectionHeader("关键日志");
        _automationPage.Controls.Add(_sectionKeyLog);

        _keyLog = new KeyLogStrip();
        _automationPage.Controls.Add(_keyLog);

        // 分组标题 + 配置卡片
        _sectionTuning = new SectionHeader(UiText.Sections.Tuning);
        _automationPage.Controls.Add(_sectionTuning);

        _config = new RaceConfigStrip(_settings);
        _config.Changed += OnConfigChanged;
        _automationPage.Controls.Add(_config);

        // Profiles 分组：让用户挑选当前生效的 events/cards/trade JSON
        _sectionProfiles = new SectionHeader(UiText.Sections.Profiles);
        _automationPage.Controls.Add(_sectionProfiles);

        _profiles = new ProfilesStrip(_settings);
        _profiles.ProfilesChanged += OnProfilesChanged;
        _automationPage.Controls.Add(_profiles);

        // Training rules / config dirs 分组：先放训练规则，再放目录入口
        _sectionTrainingRules = new SectionHeader(UiText.Sections.TrainingRules);
        _automationPage.Controls.Add(_sectionTrainingRules);

        _trainingRules = new TrainingRulesStrip(_settings.TrainingProfile);
        _trainingRules.ProfileChanged += OnTrainingProfileChanged;
        _trainingRules.EditRequested += OnTrainingRuleEditRequested;
        _trainingRules.DuplicateRequested += OnTrainingRuleDuplicateRequested;
        _automationPage.Controls.Add(_trainingRules);

        _sectionFiles = new SectionHeader(UiText.Sections.ConfigDirs);
        _automationPage.Controls.Add(_sectionFiles);

        _files = new FilesStrip();
        _automationPage.Controls.Add(_files);

        // Footer：左侧版本，右侧拖拽提示
        BuildFooter();
        SelectPage(MainPage.Automation);

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

    private void BuildPageShell()
    {
        _pageNav = new SidebarPanel();
        Controls.Add(_pageNav);

        _btnAutomationPage = CreatePageButton("自动跑马");
        _btnAutomationPage.Click += (_, _) => SelectPage(MainPage.Automation);
        _pageNav.Controls.Add(_btnAutomationPage);

        _btnNewPage = CreatePageButton("新分页");
        _btnNewPage.Click += (_, _) => SelectPage(MainPage.NewPage);
        _pageNav.Controls.Add(_btnNewPage);

        _automationPage = new Panel
        {
            BackColor = Color.Transparent,
        };
        Controls.Add(_automationPage);

        _newPage = new Panel
        {
            BackColor = Color.Transparent,
            Visible = false,
        };
        Controls.Add(_newPage);
    }

    private static SidebarTabButton CreatePageButton(string text) =>
        new()
        {
            Text = text,
            Font = RaceTheme.BoldFont(9.75F),
            ForeColor = RaceTheme.TextSecondary,
        };

    private void BuildHeroHost()
    {
        _heroHost = new HeroHostPanel
        {
            BackColor = RaceTheme.Panel,
        };
        _heroHost.Paint += HeroHost_Paint;
        _automationPage.Controls.Add(_heroHost);
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

        // 标题栏
        if (_titleBar is not null)
        {
            _titleBar.Size = new Size(clientW, TitleBarHeight);
            LayoutTitleBarChildren(clientW);
        }

        int pageTop = TitleBarHeight + GapAfterTitle;
        int pageHeight = Math.Max(0, clientH - pageTop);
        if (_pageNav is not null)
        {
            _pageNav.SetBounds(PadX, pageTop, PageNavWidth, Math.Max(PageNavButtonHeight * 2 + PageNavButtonGap, pageHeight - BottomPad));
            LayoutPageNavChildren();
        }

        int pageLeft = PadX + PageNavWidth + PageNavGap;
        int innerWidth = Math.Max(0, clientW - pageLeft - PadX);
        if (_automationPage is not null)
        {
            _automationPage.SetBounds(pageLeft, pageTop, innerWidth, pageHeight);
        }

        if (_newPage is not null)
        {
            _newPage.SetBounds(pageLeft, pageTop, innerWidth, pageHeight);
        }

        int y = 0;

        _heroCardBounds = new Rectangle(0, y, innerWidth, HeroCardHeight);
        if (_heroHost is not null)
        {
            _heroHost.SetBounds(_heroCardBounds.X, _heroCardBounds.Y, _heroCardBounds.Width, _heroCardBounds.Height);
            LayoutHeroChildren();
        }
        y = _heroCardBounds.Bottom + GapAfterHero;

        _sectionKeyLog.SetBounds(2, y, innerWidth - 4, SectionHeader.RowHeight);
        y += SectionHeader.RowHeight + GapAfterSection;

        _keyLog.SetBounds(0, y, innerWidth, KeyLogStrip.CardHeight);
        y += _keyLog.Height + GapAfterConfig;

        _sectionTuning.SetBounds(2, y, innerWidth - 4, SectionHeader.RowHeight);
        y += SectionHeader.RowHeight + GapAfterSection;

        _config.SetBounds(0, y, innerWidth, RaceConfigStrip.CardHeight);
        y += _config.Height + GapAfterConfig;

        _sectionProfiles.SetBounds(2, y, innerWidth - 4, SectionHeader.RowHeight);
        y += SectionHeader.RowHeight + GapAfterSection;

        _profiles.SetBounds(0, y, innerWidth, ProfilesStrip.CardHeight);
        y += _profiles.Height + GapAfterConfig;

        _sectionTrainingRules.SetBounds(2, y, innerWidth - 4, SectionHeader.RowHeight);
        y += SectionHeader.RowHeight + GapAfterSection;

        _trainingRules.SetBounds(0, y, innerWidth, TrainingRulesStrip.CardHeight);
        y += _trainingRules.Height + GapAfterConfig;

        _sectionFiles.SetBounds(2, y, innerWidth - 4, SectionHeader.RowHeight);
        y += SectionHeader.RowHeight + GapAfterSection;

        _files.SetBounds(0, y, innerWidth, FilesStrip.CardHeight);
        y += _files.Height + GapAfterConfig;

        // 内容总高度（footer 加上 bottom padding 之前）
        _contentHeight = pageTop + y + FooterHeight + BottomPad;

        // Footer 始终钉在底部，留出 BottomPad
        if (_footer is not null)
        {
            int footerY = Math.Max(y, pageHeight - FooterHeight - BottomPad);
            _footer.SetBounds(0, footerY, innerWidth, FooterHeight);
            LayoutFooterChildren(innerWidth);
        }
    }

    private void LayoutPageNavChildren()
    {
        _btnAutomationPage.SetBounds(0, 0, PageNavWidth, PageNavButtonHeight);
        _btnNewPage.SetBounds(0, PageNavButtonHeight + PageNavButtonGap, PageNavWidth, PageNavButtonHeight);
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
        _automationPage.Controls.Add(_footer);

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

    private void SelectPage(MainPage page)
    {
        _activePage = page;

        bool showAutomation = page == MainPage.Automation;
        if (_automationPage is not null)
        {
            _automationPage.Visible = showAutomation;
            if (showAutomation)
            {
                _automationPage.BringToFront();
            }
        }

        if (_newPage is not null)
        {
            _newPage.Visible = !showAutomation;
            if (!showAutomation)
            {
                _newPage.BringToFront();
            }
        }

        if (_btnAutomationPage is not null)
        {
            _btnAutomationPage.Active = showAutomation;
        }

        if (_btnNewPage is not null)
        {
            _btnNewPage.Active = !showAutomation;
        }

        _pageNav?.BringToFront();
        _titleBar?.BringToFront();
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

    private sealed class SidebarPanel : Panel
    {
        public SidebarPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using var pen = new Pen(RaceTheme.Divider, 1);
            e.Graphics.DrawLine(pen, Width - 1, 0, Width - 1, Height);
        }
    }

    private sealed class SidebarTabButton : Button
    {
        private bool _hover;
        private bool _pressed;
        private bool _active;

        public bool Active
        {
            get => _active;
            set
            {
                if (_active == value)
                {
                    return;
                }

                _active = value;
                Invalidate();
            }
        }

        public SidebarTabButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            BackColor = Color.Transparent;
            TextAlign = ContentAlignment.MiddleLeft;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.SupportsTransparentBackColor
                     | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            _pressed = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = Active
                ? Color.FromArgb(251, 241, 233)
                : _pressed
                    ? Color.FromArgb(244, 238, 230)
                    : _hover
                        ? Color.FromArgb(249, 246, 241)
                        : Color.Transparent;

            if (fill.A > 0)
            {
                RaceTheme.FillRoundedRect(g, rect, fill, 8);
            }

            if (Active)
            {
                using var accent = new SolidBrush(RaceTheme.Accent);
                g.FillRectangle(accent, 0, 8, 3, Height - 16);
            }

            Color fg = Active ? RaceTheme.TextPrimary : RaceTheme.TextSecondary;
            var textRect = new Rectangle(12, 0, Math.Max(0, Width - 18), Height);
            TextRenderer.DrawText(
                g,
                Text,
                Font,
                textRect,
                fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
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
    private const int WM_HOTKEY = 0x0312;
    private const int AutomationToggleHotkeyId = 0x5151;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_NOREPEAT = 0x4000;
    private const int VK_Q = 0x51;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const int LLKHF_ALTDOWN = 0x20;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Kbdllhookstruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Esc 直接关闭窗口；游戏中需要快速收起面板
        if (keyData == Keys.Escape) { Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void RegisterAutomationToggleHotkey()
    {
        if (_automationHotkeyRegistered || !IsHandleCreated)
            return;

        _automationHotkeyRegistered = RegisterHotKey(
            Handle,
            AutomationToggleHotkeyId,
            MOD_ALT | MOD_NOREPEAT,
            (uint)VK_Q);

        if (!_automationHotkeyRegistered)
        {
            int error = Marshal.GetLastWin32Error();
            Logger.Log($"[UI] Global hotkey Alt+Q registration failed: {error}");
            InstallAutomationKeyboardHook(error);
        }
    }

    private void UnregisterAutomationToggleHotkey()
    {
        if (!_automationHotkeyRegistered)
            return;

        if (!UnregisterHotKey(Handle, AutomationToggleHotkeyId))
        {
            Logger.Log($"[UI] Global hotkey Alt+Q unregister failed: {Marshal.GetLastWin32Error()}");
        }

        _automationHotkeyRegistered = false;
    }

    private void InstallAutomationKeyboardHook(int registrationError)
    {
        if (_automationKeyboardHook != IntPtr.Zero)
            return;

        _automationKeyboardHookProc ??= AutomationKeyboardHookCallback;
        _automationKeyboardHook = SetWindowsHookEx(
            WH_KEYBOARD_LL,
            _automationKeyboardHookProc,
            GetModuleHandle(null),
            0);

        if (_automationKeyboardHook == IntPtr.Zero)
        {
            Logger.Log($"[UI] Global hotkey Alt+Q fallback hook failed: {Marshal.GetLastWin32Error()} (registration error={registrationError})");
            return;
        }

        Logger.Log($"[UI] Global hotkey Alt+Q using keyboard hook fallback because RegisterHotKey failed: {registrationError}");
    }

    private void UninstallAutomationKeyboardHook()
    {
        if (_automationKeyboardHook == IntPtr.Zero)
            return;

        if (!UnhookWindowsHookEx(_automationKeyboardHook))
        {
            Logger.Log($"[UI] Global hotkey Alt+Q fallback hook unregister failed: {Marshal.GetLastWin32Error()}");
        }

        _automationKeyboardHook = IntPtr.Zero;
        _automationHotkeyPressed = false;
    }

    private IntPtr AutomationKeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var info = Marshal.PtrToStructure<Kbdllhookstruct>(lParam);
            if (HandleAutomationKeyboardHook(wParam.ToInt32(), (int)info.VkCode, (int)info.Flags))
                return (IntPtr)1;
        }

        return CallNextHookEx(_automationKeyboardHook, nCode, wParam, lParam);
    }

    private bool HandleAutomationKeyboardHook(int message, int vkCode, int flags)
    {
        if (vkCode != VK_Q)
            return false;

        bool keyUp = message == WM_KEYUP || message == WM_SYSKEYUP;
        if (keyUp)
        {
            bool wasPressed = _automationHotkeyPressed;
            _automationHotkeyPressed = false;
            return wasPressed;
        }

        bool keyDown = message == WM_KEYDOWN || message == WM_SYSKEYDOWN;
        bool altDown = (flags & LLKHF_ALTDOWN) != 0;
        if (!keyDown || !altDown)
            return false;

        if (!_automationHotkeyPressed)
        {
            _automationHotkeyPressed = true;
            ToggleAutomationFromHotkey();
        }

        return true;
    }

    private void ToggleAutomationFromHotkey()
    {
        switch (_controller.State)
        {
            case RaceState.Idle:
            case RaceState.Stopped:
                OnStartClicked();
                break;
            case RaceState.Running:
            case RaceState.Paused:
                OnStopClicked();
                break;
        }
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

    private void OnLoggerLog(string line)
    {
        if (IsDisposed || _keyLog is null)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(() => AppendKeyLogLine(line)));
            }
            catch (InvalidOperationException)
            {
            }

            return;
        }

        AppendKeyLogLine(line);
    }

    private void AppendKeyLogLine(string line)
    {
        if (!IsDisposed && _keyLog is not null)
        {
            _keyLog.TryAppendFromLogLine(line);
        }
    }

    private void ApplyState(RaceState state)
    {
        _status.ApplyState(state);
        _actions.ApplyState(state);
        Icon = UsesRunningIcon(state) ? _runningIcon : _idleIcon;
        // Idle / Stopped 时清掉活动描述，让默认副标题（"Ready to start" 等）回归
        if (state == RaceState.Idle || state == RaceState.Stopped)
            _status.SetActivity(null);
    }

    private static bool UsesRunningIcon(RaceState state) =>
        state is RaceState.Running or RaceState.Paused or RaceState.Stopping;

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
        _settings.AppraiseDifficultyMode = RaceConfig.AppraiseDifficultyMode;
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

    private void UnsubscribeLogger()
    {
        if (!_loggerSubscribed)
        {
            return;
        }

        Logger.OnLog -= OnLoggerLog;
        _loggerSubscribed = false;
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _controller.StateChanged -= OnControllerStateChanged;
        _controller.ActivityChanged -= OnActivityChanged;
        UnsubscribeLogger();
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            UnsubscribeLogger();
        }

        base.Dispose(disposing);
    }
}
