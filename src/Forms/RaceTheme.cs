using System.Drawing;
using System.Drawing.Drawing2D;

namespace SleepRunner.Forms;

internal static class RaceTheme
{
    public static readonly Color Bg = Color.FromArgb(247, 244, 239);
    public static readonly Color Panel = Color.FromArgb(251, 249, 246);
    public static readonly Color SurfaceSunken = Color.FromArgb(246, 242, 235);
    public static readonly Color SurfaceHover = Color.FromArgb(244, 238, 230);

    public static readonly Color Accent = Color.FromArgb(235, 106, 42);
    public static readonly Color AccentSoft = Color.FromArgb(242, 138, 82);
    public static readonly Color Success = Color.FromArgb(235, 106, 42);
    public static readonly Color Warn = Color.FromArgb(219, 154, 89);
    public static readonly Color Danger = Color.FromArgb(208, 116, 102);

    public static readonly Color TextPrimary = Color.FromArgb(43, 43, 43);
    public static readonly Color TextSecondary = Color.FromArgb(122, 116, 108);
    public static readonly Color TextDim = Color.FromArgb(160, 151, 141);

    public static readonly Color Border = Color.FromArgb(232, 225, 216);
    public static readonly Color Divider = Color.FromArgb(238, 232, 224);
    public static readonly Color BorderStrong = Color.FromArgb(222, 214, 204);

    public static readonly Color DisabledFill = Color.FromArgb(244, 239, 233);
    public static readonly Color DisabledFg = Color.FromArgb(183, 174, 165);

    public const string FontFamily = "Microsoft YaHei UI";
    public const string MonoFontFamily = "Consolas";

    public static Font BodyFont() => new(FontFamily, 11.25F, FontStyle.Regular);
    public static Font BoldFont(float size = 11.25F) => new(FontFamily, size, FontStyle.Bold);
    public static Font SmallFont() => new(FontFamily, 10.25F, FontStyle.Regular);
    public static Font CaptionFont() => new(FontFamily, 9.75F, FontStyle.Regular);
    public static Font SectionLabelFont() => new(FontFamily, 9.75F, FontStyle.Bold);
    public static Font NumericFont() => new(MonoFontFamily, 12.5F, FontStyle.Bold);
    public static Font LargeStatusFont() => new(FontFamily, 14.75F, FontStyle.Bold);

    public static void DrawRoundedPanel(Graphics g, Rectangle r, Color fill, Color border, int radius)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = BuildRoundedPath(r, radius);
        using var brush = new SolidBrush(fill);
        g.FillPath(brush, path);
        if (border.A > 0)
        {
            using var pen = new Pen(border, 1);
            g.DrawPath(pen, path);
        }
    }

    public static void FillRoundedRect(Graphics g, Rectangle r, Color fill, int radius)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = BuildRoundedPath(r, radius);
        using var brush = new SolidBrush(fill);
        g.FillPath(brush, path);
    }

    public static GraphicsPath BuildRoundedPath(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1));
            path.CloseFigure();
            return path;
        }

        int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height) - 1);
        var rect = new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1);
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void DrawDivider(Graphics g, int x1, int x2, int y, Color? color = null)
    {
        using var pen = new Pen(color ?? Divider, 1);
        g.DrawLine(pen, x1, y, x2, y);
    }
}
