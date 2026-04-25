using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using SleepRunner.Automation.Race.Policy.Training;
using SleepRunner.Forms;
using SleepRunner.Utils;

namespace SleepRunner.Forms.Controls;

internal sealed class TrainingRulesStrip : Control
{
    public const int CardHeight = 118;
    private const int Pad = 14;
    private const int RowGap = 8;
    private const int LabelWidth = 48;
    private const int ComboHeight = 32;
    private const int RowHeight = 34;
    private const int ButtonHeight = 36;

    public event Action<string>? ProfileChanged;
    public event Action? EditRequested;
    public event Action? DuplicateRequested;

    private readonly Label _lblProfile;
    private readonly ComboBox _cmbProfile;
    private readonly ComboChevronOverlay _arrowProfile;
    private readonly RoundedButton _btnEdit;
    private readonly RoundedButton _btnDuplicate;
    private readonly RoundedButton _btnOpenDir;
    private bool _suppressEvents;

    public TrainingRulesStrip(string preferredProfile)
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint
                 | ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.SupportsTransparentBackColor
                 | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;

        _lblProfile = new Label
        {
            Text = UiText.Training.PanelProfileLabel,
            Font = RaceTheme.SmallFont(),
            ForeColor = RaceTheme.TextSecondary,
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _cmbProfile = new WarmComboBox();
        _arrowProfile = new ComboChevronOverlay(_cmbProfile);
        _cmbProfile.SelectedIndexChanged += (_, _) => HandleSelectionChanged();

        _btnEdit = MakeButton(UiText.Actions.Edit, OnEditClicked);
        _btnDuplicate = MakeButton(UiText.Actions.Duplicate, OnDuplicateClicked);
        _btnOpenDir = MakeButton(UiText.Actions.OpenDirectory, OpenTrainingDir);

        Controls.Add(_lblProfile);
        Controls.Add(_cmbProfile);
        Controls.Add(_arrowProfile);
        Controls.Add(_btnEdit);
        Controls.Add(_btnDuplicate);
        Controls.Add(_btnOpenDir);

        Height = CardHeight;
        RefreshProfiles(preferredProfile);
        LayoutControls();
    }

    public string SelectedProfile =>
        (_cmbProfile.SelectedItem as string) ?? TrainingRuleProfileManager.DefaultProfileName;

    public void RefreshProfiles(string preferredProfile)
    {
        _suppressEvents = true;
        try
        {
            FillCombo(_cmbProfile, TrainingRuleProfileManager.ListProfiles(), preferredProfile);
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
            combo.Items.Add(TrainingRuleProfileManager.DefaultProfileName);
        }
        else
        {
            foreach (var n in names) combo.Items.Add(n);
            if (!names.Contains(preferred, StringComparer.OrdinalIgnoreCase))
                combo.Items.Insert(0, preferred);
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
        ProfileChanged?.Invoke(SelectedProfile);
    }

    private static RoundedButton MakeButton(string text, Action onClick)
    {
        var btn = new RoundedButton
        {
            Variant = RoundedButton.ButtonVariant.Ghost,
            Text = text,
            Font = RaceTheme.SmallFont(),
            Height = ButtonHeight,
            ForeColor = RaceTheme.TextSecondary,
            BackdropColor = RaceTheme.Panel,
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private sealed class WarmComboBox : ComboBox
    {
        public WarmComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            Font = RaceTheme.SmallFont();
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

    private void OnEditClicked()
    {
        EditRequested?.Invoke();
    }

    private void OnDuplicateClicked()
    {
        DuplicateRequested?.Invoke();
    }

    private static void OpenTrainingDir()
    {
        try
        {
            string dir = TrainingRuleProfileManager.TrainingDir;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                Logger.Log($"[UI] Training rules dir not found, created: {dir}");
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
            Logger.Log($"[UI] Opened training rules dir: {dir}");
        }
        catch (Exception ex)
        {
            Logger.Log($"[UI] Failed to open training rules dir: {ex.Message}");
        }
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutControls();
    }

    private void LayoutControls()
    {
        if (Width <= 0) return;

        int innerWidth = Math.Max(0, Width - Pad * 2);
        int row1Y = (Height - (RowHeight + RowGap + ButtonHeight)) / 2;
        int row2Y = row1Y + RowHeight + RowGap;

        int comboLeft = Pad + LabelWidth + RowGap;
        int comboWidth = Math.Max(70, Width - comboLeft - Pad);

        _lblProfile.SetBounds(Pad, row1Y, LabelWidth, RowHeight);
        _cmbProfile.SetBounds(comboLeft, row1Y + (RowHeight - ComboHeight) / 2, comboWidth, ComboHeight);
        _arrowProfile.SetBounds(_cmbProfile.Right - 28, _cmbProfile.Top + 1, 27, ComboHeight - 2);

        int buttonWidth = (innerWidth - RowGap * 2) / 3;
        if (buttonWidth < 30) buttonWidth = 30;

        _btnEdit.SetBounds(Pad, row2Y, buttonWidth, ButtonHeight);
        _btnDuplicate.SetBounds(Pad + buttonWidth + RowGap, row2Y, buttonWidth, ButtonHeight);
        _btnOpenDir.SetBounds(Pad + (buttonWidth + RowGap) * 2, row2Y, Math.Max(30, innerWidth - (buttonWidth + RowGap) * 2), ButtonHeight);
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
