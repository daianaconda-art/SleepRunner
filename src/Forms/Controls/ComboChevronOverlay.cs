using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SleepRunner.Forms.Controls;

internal sealed class ComboChevronOverlay : Control
{
    private readonly ComboBox _combo;
    private bool _hover;
    private bool _pressed;

    public ComboChevronOverlay(ComboBox combo)
    {
        _combo = combo;
        SetStyle(ControlStyles.UserPaint
                 | ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw
                 | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = RaceTheme.Panel;
        Cursor = Cursors.Hand;
        TabStop = false;
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

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_pressed && e.Button == MouseButtons.Left)
        {
            _combo.Focus();
            _combo.DroppedDown = true;
        }

        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        Color fill = _pressed
            ? Color.FromArgb(244, 238, 230)
            : _hover
                ? Color.FromArgb(248, 243, 237)
                : RaceTheme.Panel;

        using var brush = new SolidBrush(fill);
        e.Graphics.FillRectangle(brush, ClientRectangle);

        using var divider = new Pen(RaceTheme.Border, 1);
        e.Graphics.DrawLine(divider, 0, 4, 0, Height - 5);

        int cx = Width / 2;
        int cy = Height / 2 + 1;
        using var pen = new Pen(RaceTheme.TextSecondary, 1.6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };
        e.Graphics.DrawLine(pen, cx - 4, cy - 2, cx, cy + 2);
        e.Graphics.DrawLine(pen, cx, cy + 2, cx + 4, cy - 2);
    }
}
