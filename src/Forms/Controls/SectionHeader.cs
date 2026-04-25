using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace SleepRunner.Forms.Controls;

/// <summary>
/// 卡片之间的小型分组标签，例如 "TUNING"、"WINDOW"
/// 一行 18px，左侧短主色竖线 + 全大写小字
/// </summary>
internal sealed class SectionHeader : Control
{
    public const int RowHeight = 24;

    public SectionHeader(string text)
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint
                 | ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        ForeColor = RaceTheme.TextSecondary;
        Font = RaceTheme.SectionLabelFont();
        Text = text;
        Height = RowHeight;
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // 左侧短色条
        using (var brush = new SolidBrush(RaceTheme.Accent))
            g.FillEllipse(brush, 0, (Height - 6) / 2, 6, 6);

        var textRect = new Rectangle(14, 0, Width - 14, Height);
        TextRenderer.DrawText(g, Text, Font, textRect, ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}
