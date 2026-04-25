using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SleepRunner.Forms.Controls;

/// <summary>
/// 状态指示圆点 + halo 光晕；颜色由调用方按 RaceState 切换
/// </summary>
internal sealed class StatusDot : Control
{
    private Color _dotColor = Color.Gray;
    public Color DotColor
    {
        get => _dotColor;
        set { _dotColor = value; Invalidate(); }
    }

    public StatusDot()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int size = Math.Min(Width, Height) - 4;
        var rect = new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);

        using (var halo = new SolidBrush(Color.FromArgb(80, _dotColor)))
            g.FillEllipse(halo, Rectangle.Inflate(rect, 3, 3));

        using var brush = new SolidBrush(_dotColor);
        g.FillEllipse(brush, rect);
    }
}
