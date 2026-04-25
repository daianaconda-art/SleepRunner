using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using SleepRunner.Automation.Race.Policy;
using SleepRunner.Forms;
using SleepRunner.Utils;

namespace SleepRunner.Forms.Controls;

internal sealed class FilesStrip : Control
{
    public const int CardHeight = 78;
    private const int Pad = 12;
    private const int RowGap = 8;
    private const int BtnHeight = 40;

    private RoundedButton? _btnEvents;
    private RoundedButton? _btnCards;
    private RoundedButton? _btnTrade;

    public FilesStrip()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint
                 | ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.SupportsTransparentBackColor
                 | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;

        _btnEvents = MakeButton(UiText.Files.EventsDir, UiText.Files.EventsTooltip, () => RaceProfileManager.EventsDir);
        _btnCards = MakeButton(UiText.Files.CardsDir, UiText.Files.CardsTooltip, () => RaceProfileManager.CardsDir);
        _btnTrade = MakeButton(UiText.Files.TradeDir, UiText.Files.TradeTooltip, () => RaceProfileManager.TradeDir);

        Controls.Add(_btnEvents);
        Controls.Add(_btnCards);
        Controls.Add(_btnTrade);

        Height = CardHeight;
        LayoutButtons();
        RefreshButtonStates();
    }

    private static RoundedButton MakeButton(string text, string tooltip, Func<string> dirProvider)
    {
        var btn = new RoundedButton
        {
            Variant = RoundedButton.ButtonVariant.Ghost,
            Text = text,
            Font = RaceTheme.SmallFont(),
            Height = BtnHeight,
            ForeColor = RaceTheme.TextSecondary,
            BackdropColor = RaceTheme.Panel,
            CornerRadius = 12,
        };

        btn.Click += (_, _) => OpenInExplorer(dirProvider());
        var tip = new ToolTip { InitialDelay = 350, ReshowDelay = 100 };
        tip.SetToolTip(btn, tooltip + Environment.NewLine + dirProvider());
        return btn;
    }

    private static void OpenInExplorer(string dir)
    {
        try
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                Logger.Log($"[UI] Profile dir not found, created: {dir}");
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
            Logger.Log($"[UI] Opened profile dir: {dir}");
        }
        catch (Exception ex)
        {
            Logger.Log($"[UI] Failed to open dir '{dir}': {ex.Message}");
        }
    }

    public void RefreshButtonStates()
    {
        if (_btnEvents is not null) _btnEvents.Enabled = true;
        if (_btnCards is not null) _btnCards.Enabled = true;
        if (_btnTrade is not null) _btnTrade.Enabled = true;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutButtons();
    }

    private void LayoutButtons()
    {
        var btnEvents = _btnEvents;
        var btnCards = _btnCards;
        var btnTrade = _btnTrade;
        if (btnEvents is null || btnCards is null || btnTrade is null) return;
        if (Width <= 0) return;

        int innerWidth = Math.Max(0, Width - Pad * 2);
        int eachWidth = (innerWidth - RowGap * 2) / 3;
        if (eachWidth < 30) eachWidth = 30;

        int y = (Height - BtnHeight) / 2;
        int x = Pad;
        btnEvents.SetBounds(x, y, eachWidth, BtnHeight);
        x += eachWidth + RowGap;
        btnCards.SetBounds(x, y, eachWidth, BtnHeight);
        x += eachWidth + RowGap;
        btnTrade.SetBounds(x, y, eachWidth, BtnHeight);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width, Height);
        RaceTheme.FillRoundedRect(g, rect, RaceTheme.Panel, 14);
        using var path = RaceTheme.BuildRoundedPath(rect, 14);
        using var pen = new Pen(RaceTheme.Border, 1);
        g.DrawPath(pen, path);
    }
}
