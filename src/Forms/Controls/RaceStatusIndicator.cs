using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using SleepRunner.Automation;
using SleepRunner.Forms;

namespace SleepRunner.Forms.Controls;

internal sealed class RaceStatusIndicator : Control
{
    private RaceState _state = RaceState.Idle;
    private string _title = UiText.Status.IdleTitle;
    private string _subtitle = UiText.Status.IdleSubtitle;
    private string _activity = string.Empty;
    private Color _color = RaceTheme.Accent;

    public const int CardHeight = 78;

    public RaceStatusIndicator()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint
                 | ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.SupportsTransparentBackColor
                 | ControlStyles.ResizeRedraw, true);
        BackColor = RaceTheme.Panel;
        Height = CardHeight;
    }

    public RaceState State => _state;

    public void ApplyState(RaceState state)
    {
        _state = state;
        switch (state)
        {
            case RaceState.Idle:
                _color = RaceTheme.TextDim;
                _title = UiText.Status.IdleTitle;
                _subtitle = UiText.Status.IdleSubtitle;
                break;
            case RaceState.Stopped:
                _color = RaceTheme.TextDim;
                _title = UiText.Status.StoppedTitle;
                _subtitle = UiText.Status.StoppedSubtitle;
                break;
            case RaceState.Running:
                _color = RaceTheme.Success;
                _title = UiText.Status.RunningTitle;
                _subtitle = UiText.Status.RunningSubtitle;
                break;
            case RaceState.Paused:
                _color = RaceTheme.Warn;
                _title = UiText.Status.PausedTitle;
                _subtitle = UiText.Status.PausedSubtitle;
                break;
            case RaceState.Stopping:
                _color = RaceTheme.Danger;
                _title = UiText.Status.StoppingTitle;
                _subtitle = UiText.Status.StoppingSubtitle;
                break;
        }

        Invalidate();
    }

    public void SetActivity(string? text)
    {
        string value = text ?? string.Empty;
        if (_activity == value) return;
        _activity = value;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        const int dotSize = 12;
        var dotRect = new Rectangle(12, 16, dotSize, dotSize);
        using (var halo = new SolidBrush(Color.FromArgb(42, _color)))
            g.FillEllipse(halo, Rectangle.Inflate(dotRect, 5, 5));
        using (var brush = new SolidBrush(_color))
            g.FillEllipse(brush, dotRect);

        var titleRect = new Rectangle(34, 10, Width - 40, 28);
        var subRect = new Rectangle(34, 38, Width - 40, 22);

        TextRenderer.DrawText(g, _title, RaceTheme.BoldFont(14F), titleRect,
            RaceTheme.TextPrimary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

        string subtitle = string.IsNullOrEmpty(_activity) ? _subtitle : _activity;
        TextRenderer.DrawText(g, subtitle, RaceTheme.SmallFont(), subRect,
            RaceTheme.TextSecondary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}
