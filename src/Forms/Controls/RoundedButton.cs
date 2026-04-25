using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace SleepRunner.Forms.Controls;

/// <summary>
/// 圆角按钮：支持 Primary（实心主色） / Secondary（深底 + 描边） / Ghost（透明） 三种风格
/// 同时支持在文字左侧绘制一个矢量小图标（Play / Stop / None）
/// </summary>
internal sealed class RoundedButton : Button
{
    public enum ButtonVariant { Primary, Secondary, Ghost }
    public enum LeadingIcon { None, Play, Stop }

    public Color AccentColor { get; set; } = RaceTheme.Accent;
    public ButtonVariant Variant { get; set; } = ButtonVariant.Primary;
    public LeadingIcon Icon { get; set; } = LeadingIcon.None;
    public int CornerRadius { get; set; } = 12;

    /// <summary>
    /// Ghost 变体的"基底色"。BackColor=Transparent + OptimizedDoubleBuffer 的组合
    /// 实测无法稳定让父控件背景渗透进 buffer（会出现旧帧文字残影 / 兄弟控件误绘）。
    /// 解决办法：让宿主显式告诉按钮自己坐在什么颜色的卡片上（如 RaceTheme.Panel），
    /// 按钮 Ghost 非 hover 时直接用此色填底，hover/pressed 的半透明白叠在上面就稳定了。
    /// 默认 Transparent 表示沿用旧行为（不填底）。
    /// </summary>
    public Color BackdropColor { get; set; } = Color.Transparent;

    private bool _hover;
    private bool _pressed;

    public RoundedButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseOverBackColor = Color.Transparent;
        FlatAppearance.MouseDownBackColor = Color.Transparent;
        BackColor = Color.Transparent;
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint
                 | ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.SupportsTransparentBackColor
                 | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateClipRegion();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateClipRegion();
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs mevent) { _pressed = true; Invalidate(); base.OnMouseDown(mevent); }
    protected override void OnMouseUp(MouseEventArgs mevent) { _pressed = false; Invalidate(); base.OnMouseUp(mevent); }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        ResolveColors(out var fill, out var border, out var fg);
        Color backdrop = ResolveBackdropColor();

        // 先用宿主底色清掉当前 buffer，避免 hover / resize 时边角出现残影或黑角。
        if (backdrop.A > 0)
            RaceTheme.FillRoundedRect(g, rect, backdrop, CornerRadius);

        if (fill.A > 0)
            RaceTheme.FillRoundedRect(g, rect, fill, CornerRadius);

        if (border.A > 0)
        {
            using var path = RaceTheme.BuildRoundedPath(rect, CornerRadius);
            using var pen = new Pen(border, 1);
            g.DrawPath(pen, path);
        }

        // 顶端微高光（仅 Primary 实心态），让按钮看起来略带凸起感
        if (Enabled && Variant == ButtonVariant.Primary && !_pressed)
        {
            var highlight = new Rectangle(2, 1, Width - 4, Height / 2);
            using var brush = new LinearGradientBrush(highlight,
                Color.FromArgb(36, 255, 255, 255), Color.FromArgb(0, 255, 255, 255),
                LinearGradientMode.Vertical);
            using var path = RaceTheme.BuildRoundedPath(highlight, CornerRadius - 1);
            g.FillPath(brush, path);
        }

        // 内容（图标 + 文字）整体居中
        DrawContent(g, fg);
    }

    private void ResolveColors(out Color fill, out Color border, out Color fg)
    {
        if (!Enabled)
        {
            fill = RaceTheme.DisabledFill;
            border = RaceTheme.Border;
            fg = RaceTheme.DisabledFg;
            return;
        }

        switch (Variant)
        {
            case ButtonVariant.Primary:
                if (UsesDefaultAccent)
                {
                    fill = _pressed ? Color.FromArgb(229, 98, 35)
                         : _hover ? RaceTheme.AccentSoft
                         : RaceTheme.Accent;
                    border = Color.FromArgb(225, 117, 67);
                }
                else
                {
                    fill = _pressed ? Darken(AccentColor, 0.15f)
                         : _hover ? Lighten(AccentColor, 0.08f)
                         : AccentColor;
                    border = Darken(AccentColor, 0.2f);
                }
                fg = Color.White;
                break;

            case ButtonVariant.Secondary:
                fill = _pressed ? Color.FromArgb(245, 241, 235)
                     : _hover ? Color.FromArgb(249, 246, 241)
                     : RaceTheme.Panel;
                border = UsesDefaultAccent ? RaceTheme.Border : Darken(AccentColor, 0.15f);
                fg = UsesDefaultAccent ? ForeColor : AccentColor;
                break;

            case ButtonVariant.Ghost:
            default:
                fill = _pressed ? Color.FromArgb(245, 239, 232)
                     : _hover ? Color.FromArgb(249, 245, 240)
                     : Color.Transparent;
                border = RaceTheme.Border;
                fg = ForeColor;
                break;
        }
    }

    private bool UsesDefaultAccent => AccentColor.ToArgb() == RaceTheme.Accent.ToArgb();

    private Color ResolveBackdropColor()
    {
        if (BackdropColor.A > 0)
        {
            return BackdropColor;
        }

        return Parent?.BackColor ?? Color.Transparent;
    }

    private void UpdateClipRegion()
    {
        if (Width <= 1 || Height <= 1)
        {
            return;
        }

        using var path = RaceTheme.BuildRoundedPath(new Rectangle(0, 0, Width, Height), CornerRadius);
        var oldRegion = Region;
        Region = new Region(path);
        oldRegion?.Dispose();
    }

    private static Color Darken(Color c, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(c.A,
            (int)(c.R * (1 - amount)),
            (int)(c.G * (1 - amount)),
            (int)(c.B * (1 - amount)));
    }

    private static Color Lighten(Color c, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(c.A,
            (int)(c.R + (255 - c.R) * amount),
            (int)(c.G + (255 - c.G) * amount),
            (int)(c.B + (255 - c.B) * amount));
    }

    private void DrawContent(Graphics g, Color fg)
    {
        const int iconSize = 10;
        int iconWidth = Icon == LeadingIcon.None ? 0 : iconSize + 8;
        var textSize = TextRenderer.MeasureText(g, Text, Font);
        int contentWidth = iconWidth + textSize.Width;
        int startX = (Width - contentWidth) / 2;
        int centerY = Height / 2;

        if (Icon != LeadingIcon.None)
        {
            DrawLeadingIcon(g, fg, new Point(startX, centerY - iconSize / 2), iconSize);
            startX += iconWidth;
        }

        var textRect = new Rectangle(startX, 0, textSize.Width, Height);
        TextRenderer.DrawText(g, Text, Font, textRect, fg,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
    }

    private void DrawLeadingIcon(Graphics g, Color fg, Point origin, int size)
    {
        using var brush = new SolidBrush(fg);
        switch (Icon)
        {
            case LeadingIcon.Play:
            {
                var pts = new Point[]
                {
                    new(origin.X, origin.Y),
                    new(origin.X + size, origin.Y + size / 2),
                    new(origin.X, origin.Y + size),
                };
                g.FillPolygon(brush, pts);
                break;
            }
            case LeadingIcon.Stop:
            {
                var rect = new Rectangle(origin.X, origin.Y, size, size);
                g.FillRectangle(brush, rect);
                break;
            }
        }
    }
}
