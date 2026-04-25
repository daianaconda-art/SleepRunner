using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace SleepRunner.Forms.Controls;

/// <summary>
/// 双段切换控件：[ Attack | Survival ] 风格
///
/// - 选中段填实主色，未选中段透明带文字
/// - 用一个数组承载段文本，点击哪段就切换 SelectedIndex
/// - 适合 2~3 段的离散选项；超过 3 段建议改用下拉
/// </summary>
internal sealed class SegmentedToggle : Control
{
    private string[] _segments = Array.Empty<string>();
    private int _selectedIndex;
    private int _hoverIndex = -1;

    public event Action? SelectedIndexChanged;

    public string[] Segments
    {
        get => _segments;
        set { _segments = value ?? Array.Empty<string>(); Invalidate(); }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int v = Math.Clamp(value, 0, Math.Max(0, _segments.Length - 1));
            if (_selectedIndex == v) return;
            _selectedIndex = v;
            Invalidate();
            SelectedIndexChanged?.Invoke();
        }
    }

    public SegmentedToggle()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint
                 | ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.SupportsTransparentBackColor
                 | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        ForeColor = RaceTheme.TextPrimary;
        Font = RaceTheme.BoldFont(8.5F);
        Size = new Size(140, 26);
    }

    private int SegmentWidth => _segments.Length == 0 ? Width : Width / _segments.Length;

    private int HitTest(Point p)
    {
        if (_segments.Length == 0) return -1;
        int idx = p.X / SegmentWidth;
        return Math.Clamp(idx, 0, _segments.Length - 1);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int idx = HitTest(e.Location);
        if (idx != _hoverIndex) { _hoverIndex = idx; Invalidate(); }
        Cursor = Cursors.Hand;
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hoverIndex = -1; Invalidate(); base.OnMouseLeave(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        int idx = HitTest(e.Location);
        if (idx >= 0) SelectedIndex = idx;
        base.OnMouseClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width, Height);
        RaceTheme.FillRoundedRect(g, rect, RaceTheme.SurfaceSunken, 6);
        using (var path = RaceTheme.BuildRoundedPath(rect, 6))
        using (var pen = new Pen(RaceTheme.Border, 1))
            g.DrawPath(pen, path);

        if (_segments.Length == 0) return;

        int segW = SegmentWidth;

        // 选中段：实色填充的内部圆角条
        var selRect = new Rectangle(_selectedIndex * segW + 2, 2, segW - 4, Height - 4);
        RaceTheme.FillRoundedRect(g, selRect, RaceTheme.Accent, 4);

        // 文字
        for (int i = 0; i < _segments.Length; i++)
        {
            var area = new Rectangle(i * segW, 0, segW, Height);
            Color fg;
            if (i == _selectedIndex) fg = Color.White;
            else if (i == _hoverIndex) fg = RaceTheme.TextPrimary;
            else fg = RaceTheme.TextSecondary;

            TextRenderer.DrawText(g, _segments[i], Font, area, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }
}
