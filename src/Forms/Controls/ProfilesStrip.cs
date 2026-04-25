using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SleepRunner.Automation.Race.Policy;
using SleepRunner.Forms;
using SleepRunner.Utils;

namespace SleepRunner.Forms.Controls;

/// <summary>
/// "Profiles" 卡片：三组下拉框，分别选择当前使用的 events / cards / trade JSON
///
/// 设计目标：
///   - 让用户能在 UI 一眼看到当前生效的 profile（例如攻击 vs 生存）
///   - 切换后立即同步到 RaceProfileManager + UserSettings，下一帧策略即重读
///   - 目录里没有 *.json 时下拉退化为单条 "default" 占位，避免空 ComboBox 的歧义
///   - 与 FilesStrip / RaceConfigStrip 同款圆角面板风格
/// </summary>
internal sealed class ProfilesStrip : Control
{
    public const int CardHeight = 132;
    private const int Pad = 14;
    private const int RowGap = 8;
    private const int LabelWidth = 54;
    private const int ComboHeight = 32;
    private const int RowHeight = 34;

    /// <summary>三个下拉的当前选择变化时回传新值（events/cards/trade name）</summary>
    public event Action<string, string, string>? ProfilesChanged;

    private readonly Label _lblEvents;
    private readonly Label _lblCards;
    private readonly Label _lblTrade;
    private readonly ComboBox _cmbEvents;
    private readonly ComboBox _cmbCards;
    private readonly ComboBox _cmbTrade;
    private readonly ComboChevronOverlay _arrowEvents;
    private readonly ComboChevronOverlay _arrowCards;
    private readonly ComboChevronOverlay _arrowTrade;
    private readonly UserSettings _settings;

    // 防抖：refresh 列表时 SelectedIndexChanged 也会触发，借此抑制写回 settings
    private bool _suppressEvents;

    public ProfilesStrip(UserSettings settings)
    {
        _settings = settings;
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint
                 | ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.SupportsTransparentBackColor
                 | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;

        _lblEvents = MakeLabel(UiText.Profiles.Events);
        _lblCards = MakeLabel(UiText.Profiles.Cards);
        _lblTrade = MakeLabel(UiText.Profiles.Trade);
        _cmbEvents = MakeCombo();
        _cmbCards = MakeCombo();
        _cmbTrade = MakeCombo();
        _arrowEvents = new ComboChevronOverlay(_cmbEvents);
        _arrowCards = new ComboChevronOverlay(_cmbCards);
        _arrowTrade = new ComboChevronOverlay(_cmbTrade);

        _cmbEvents.SelectedIndexChanged += (_, _) => HandleSelectionChanged();
        _cmbCards.SelectedIndexChanged += (_, _) => HandleSelectionChanged();
        _cmbTrade.SelectedIndexChanged += (_, _) => HandleSelectionChanged();

        Controls.Add(_lblEvents);
        Controls.Add(_lblCards);
        Controls.Add(_lblTrade);
        Controls.Add(_cmbEvents);
        Controls.Add(_cmbCards);
        Controls.Add(_cmbTrade);
        Controls.Add(_arrowEvents);
        Controls.Add(_arrowCards);
        Controls.Add(_arrowTrade);

        Height = CardHeight;
        RefreshProfileLists();
        LayoutRows();
    }

    /// <summary>外部可调（如窗口激活时）刷新目录列表，自动保留当前选中</summary>
    public void RefreshProfileLists()
    {
        _suppressEvents = true;
        try
        {
            FillCombo(_cmbEvents, RaceProfileManager.ListEventsProfiles(), _settings.EventsProfile);
            FillCombo(_cmbCards, RaceProfileManager.ListCardsProfiles(), _settings.CardsProfile);
            FillCombo(_cmbTrade, RaceProfileManager.ListTradeProfiles(), _settings.TradeProfile);
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private static void FillCombo(ComboBox combo, IReadOnlyList<string> names, string preferred)
    {
        combo.Items.Clear();
        if (names.Count == 0)
        {
            combo.Items.Add(RaceProfileManager.DefaultProfileName);
        }
        else
        {
            foreach (var n in names) combo.Items.Add(n);
            if (!names.Contains(preferred, StringComparer.OrdinalIgnoreCase))
                combo.Items.Insert(0, preferred); // 保留 settings 里写过的名字，便于用户后续放上来
        }

        int idx = -1;
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (string.Equals(combo.Items[i] as string, preferred, StringComparison.OrdinalIgnoreCase))
            {
                idx = i;
                break;
            }
        }
        combo.SelectedIndex = idx >= 0 ? idx : 0;
    }

    private void HandleSelectionChanged()
    {
        if (_suppressEvents) return;
        string events = (_cmbEvents.SelectedItem as string) ?? RaceProfileManager.DefaultProfileName;
        string cards = (_cmbCards.SelectedItem as string) ?? RaceProfileManager.DefaultProfileName;
        string trade = (_cmbTrade.SelectedItem as string) ?? RaceProfileManager.DefaultProfileName;

        // 推到 RaceProfileManager；同名 set 是 no-op，不会触发多余热重载
        RaceProfileManager.SetEventsProfile(events);
        RaceProfileManager.SetCardsProfile(cards);
        RaceProfileManager.SetTradeProfile(trade);

        ProfilesChanged?.Invoke(events, cards, trade);
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text,
        Font = RaceTheme.CaptionFont(),
        ForeColor = RaceTheme.TextSecondary,
        BackColor = Color.Transparent,
        AutoSize = false,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static ComboBox MakeCombo() => new WarmComboBox();

    private sealed class WarmComboBox : ComboBox
    {
        public WarmComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            Font = RaceTheme.CaptionFont();
            FlatStyle = FlatStyle.Flat;
            IntegralHeight = false;
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 26;
            BackColor = RaceTheme.Panel;
            ForeColor = RaceTheme.TextPrimary;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index >= 0)
            {
                string text = Items[e.Index]?.ToString() ?? string.Empty;
                TextRenderer.DrawText(e.Graphics, text, Font, e.Bounds, e.ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            }

            e.DrawFocusRectangle();
            base.OnDrawItem(e);
        }
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutRows();
    }

    /// <summary>
    /// 三行竖排：[label] [combo 占满剩余宽]，每行 RowHeight；构造期触发也安全
    /// </summary>
    private void LayoutRows()
    {
        if (Width <= 0) return;
        if (_lblEvents is null) return;

        int x = Pad;
        int comboLeft = x + LabelWidth + RowGap;
        int comboWidth = Math.Max(60, Width - comboLeft - Pad);

        int y = (Height - (RowHeight * 3 + RowGap * 2)) / 2;

        LayoutRow(_lblEvents, _cmbEvents, _arrowEvents, x, comboLeft, comboWidth, y);
        y += RowHeight + RowGap;
        LayoutRow(_lblCards, _cmbCards, _arrowCards, x, comboLeft, comboWidth, y);
        y += RowHeight + RowGap;
        LayoutRow(_lblTrade, _cmbTrade, _arrowTrade, x, comboLeft, comboWidth, y);
    }

    private static void LayoutRow(Label label, ComboBox combo, Control arrow, int labelX, int comboX, int comboWidth, int rowY)
    {
        int comboY = rowY + (RowHeight - ComboHeight) / 2;
        label.SetBounds(labelX, rowY, LabelWidth, RowHeight);
        combo.SetBounds(comboX, comboY, comboWidth, ComboHeight);
        arrow.SetBounds(combo.Right - 28, comboY + 1, 27, ComboHeight - 2);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width, Height);
        RaceTheme.FillRoundedRect(g, rect, RaceTheme.Panel, 8);
        using var path = RaceTheme.BuildRoundedPath(rect, 8);
        using var pen = new Pen(RaceTheme.Border, 1);
        g.DrawPath(pen, path);
    }
}
