using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SleepRunner.Forms.Controls;

/// <summary>
/// 标题栏图标按钮：自绘 28×28 圆角小按钮，无文字
/// 通过 Glyph 枚举切换内部矢量图标，避免依赖 Segoe MDL2 字体
/// </summary>
internal sealed class IconButton : Control
{
    public enum Glyph
    {
        Pin,
        PinFilled,
        Close,
        Minimize,
    }

    private bool _hover;
    private bool _pressed;
    private Glyph _icon = Glyph.Close;
    private bool _active;

    /// <summary>选中态高亮（如：Pin 已开启）</summary>
    public bool Active
    {
        get => _active;
        set { _active = value; Invalidate(); }
    }

    public Glyph Icon
    {
        get => _icon;
        set { _icon = value; Invalidate(); }
    }

    /// <summary>悬停时是否使用危险色（关闭按钮）</summary>
    public bool DangerHover { get; set; }

    public IconButton()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint
                 | ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(30, 28);
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs mevent) { _pressed = true; Invalidate(); base.OnMouseDown(mevent); }
    protected override void OnMouseUp(MouseEventArgs mevent) { _pressed = false; Invalidate(); base.OnMouseUp(mevent); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width, Height);

        // 背景态：active > pressed > hover
        Color? fill = null;
        if (_pressed) fill = DangerHover ? Color.FromArgb(232, 121, 101) : Color.FromArgb(248, 239, 232);
        else if (_hover) fill = DangerHover ? Color.FromArgb(239, 132, 113) : Color.FromArgb(251, 243, 236);
        else if (_active) fill = Color.FromArgb(252, 240, 231);

        if (fill.HasValue)
            RaceTheme.FillRoundedRect(g, rect, fill.Value, 5);

        Color fg;
        if (_hover && DangerHover) fg = Color.White;
        else if (_active) fg = RaceTheme.Accent;
        else fg = RaceTheme.TextSecondary;

        DrawIcon(g, rect, fg);
    }

    private void DrawIcon(Graphics g, Rectangle r, Color color)
    {
        var center = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
        using var pen = new Pen(color, 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(color);

        switch (_icon)
        {
            case Glyph.Close:
            {
                int s = 4;
                g.DrawLine(pen, center.X - s, center.Y - s, center.X + s, center.Y + s);
                g.DrawLine(pen, center.X + s, center.Y - s, center.X - s, center.Y + s);
                break;
            }
            case Glyph.Minimize:
            {
                int s = 5;
                g.DrawLine(pen, center.X - s, center.Y + 3, center.X + s, center.Y + 3);
                break;
            }
            case Glyph.Pin:
            case Glyph.PinFilled:
            {
                // 简化的图钉造型：上方矩形帽 + 一个三角钉身 + 一根针脚
                bool filled = _icon == Glyph.PinFilled || _active;
                var capTop = new Rectangle(center.X - 4, center.Y - 6, 8, 3);
                var body = new Point[]
                {
                    new(center.X - 5, center.Y - 3),
                    new(center.X + 5, center.Y - 3),
                    new(center.X + 2, center.Y + 2),
                    new(center.X - 2, center.Y + 2),
                };
                if (filled)
                {
                    g.FillRectangle(brush, capTop);
                    g.FillPolygon(brush, body);
                }
                else
                {
                    g.DrawRectangle(pen, capTop);
                    g.DrawPolygon(pen, body);
                }
                g.DrawLine(pen, center.X, center.Y + 2, center.X, center.Y + 6);
                break;
            }
        }
    }
}
