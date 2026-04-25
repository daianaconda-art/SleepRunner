using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SleepRunner.Automation.Race;
using SleepRunner.Forms;
using SleepRunner.Utils;

namespace SleepRunner.Forms.Controls;

internal sealed class RaceConfigStrip : Control
{
    private NumericStepper? _stepSpeed;
    private NumericStepper? _stepClick;
    private ConfigRow? _rowSpeed;
    private ConfigRow? _rowClick;

    public const int VisibleRowCount = 2;
    public const int CardHeight = 136;

    public event Action? Changed;

    public RaceConfigStrip(UserSettings settings)
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint
                 | ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.SupportsTransparentBackColor
                 | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;

        var stepSpeed = new NumericStepper
        {
            Minimum = 0.5m,
            Maximum = 2.0m,
            Increment = 0.1m,
            DecimalPlaces = 1,
            Suffix = "x",
            Value = Math.Clamp((decimal)settings.WaitMultiplier, 0.5m, 2.0m),
            Width = 132,
        };
        stepSpeed.ValueChanged += () =>
        {
            double v = (double)stepSpeed.Value;
            if (Math.Abs(v - RaceConfig.WaitMultiplier) > 0.001) RaceConfig.WaitMultiplier = v;
            Changed?.Invoke();
        };
        var rowSpeed = new ConfigRow(UiText.Config.WaitMultiplierTitle, UiText.Config.WaitMultiplierHint);
        rowSpeed.SetEditor(stepSpeed);

        var stepClick = new NumericStepper
        {
            Minimum = 0.3m,
            Maximum = 2.0m,
            Increment = 0.1m,
            DecimalPlaces = 1,
            Suffix = "x",
            Value = Math.Clamp((decimal)settings.ClickSpeedMultiplier, 0.3m, 2.0m),
            Width = 132,
        };
        stepClick.ValueChanged += () =>
        {
            double v = (double)stepClick.Value;
            if (Math.Abs(v - RaceConfig.ClickSpeedMultiplier) > 0.001) RaceConfig.ClickSpeedMultiplier = v;
            Changed?.Invoke();
        };
        var rowClick = new ConfigRow(UiText.Config.ClickSpeedTitle, UiText.Config.ClickSpeedHint);
        rowClick.SetEditor(stepClick);

        _stepSpeed = stepSpeed;
        _stepClick = stepClick;
        _rowSpeed = rowSpeed;
        _rowClick = rowClick;

        Controls.Add(rowSpeed);
        Controls.Add(rowClick);

        Height = CardHeight;
        LayoutRows();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutRows();
    }

    private void LayoutRows()
    {
        const int pad = 8;
        var rows = new[] { _rowSpeed, _rowClick };
        foreach (var row in rows)
        {
            if (row is null) return;
        }

        int y = pad;
        foreach (var row in rows)
        {
            row!.SetBounds(0, y, Width, ConfigRow.RowHeight);
            y += ConfigRow.RowHeight;
        }

        Height = y + pad;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width, Height);
        RaceTheme.FillRoundedRect(g, rect, RaceTheme.Panel, 8);
        using (var path = RaceTheme.BuildRoundedPath(rect, 8))
        using (var pen = new Pen(RaceTheme.Border, 1))
            g.DrawPath(pen, path);

        var rows = new[] { _rowSpeed };
        foreach (var row in rows)
        {
            if (row is null) continue;
            int y = row.Bottom;
            RaceTheme.DrawDivider(g, 14, Width - 14, y);
        }
    }
}
