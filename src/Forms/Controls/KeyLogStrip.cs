using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace SleepRunner.Forms.Controls;

internal sealed class KeyLogStrip : Control
{
    private const int MaxEntries = 5;
    private const int Pad = 10;
    private const int LineHeight = 18;
    private readonly List<string> _entries = [];

    public const int CardHeight = 108;

    internal IReadOnlyList<string> Entries => _entries;

    public KeyLogStrip()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint
                 | ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.SupportsTransparentBackColor
                 | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        ForeColor = RaceTheme.TextSecondary;
        Font = new Font(RaceTheme.MonoFontFamily, 8.75F, FontStyle.Regular);
        Height = CardHeight;
    }

    public void TryAppendFromLogLine(string line)
    {
        if (!TryFormatEntry(line, out string entry))
        {
            return;
        }

        if (_entries.Count > 0 && string.Equals(_entries[^1], entry, StringComparison.Ordinal))
        {
            return;
        }

        _entries.Add(entry);
        while (_entries.Count > MaxEntries)
        {
            _entries.RemoveAt(0);
        }

        Invalidate();
    }

    internal static bool TryFormatEntry(string line, out string entry)
    {
        entry = "";
        if (string.IsNullOrWhiteSpace(line) ||
            !line.Contains("[Race:TrainingSelect]", StringComparison.Ordinal))
        {
            return false;
        }

        string message = StripTimestamp(line);
        int tagIndex = message.IndexOf("[Race:TrainingSelect]", StringComparison.Ordinal);
        if (tagIndex >= 0)
        {
            message = message[(tagIndex + "[Race:TrainingSelect]".Length)..].Trim();
        }

        if (message.StartsWith("Slot ", StringComparison.Ordinal))
        {
            return false;
        }

        if (message.Contains("Full scan snapshot:", StringComparison.Ordinal))
        {
            entry = message.Replace("Full scan snapshot:", "Scan:", StringComparison.Ordinal);
            return true;
        }

        if (message.Contains("Lazy scan snapshot:", StringComparison.Ordinal))
        {
            entry = message.Replace("Lazy scan snapshot:", "Scan:", StringComparison.Ordinal);
            return true;
        }

        if (message.Contains(": icons=", StringComparison.Ordinal) &&
            message.Contains("failRate=", StringComparison.Ordinal))
        {
            entry = message.Trim();
            return true;
        }

        if (message.StartsWith("Rule evaluation:", StringComparison.Ordinal))
        {
            entry = message.Replace("Rule evaluation:", "Decision:", StringComparison.Ordinal);
            return true;
        }

        if (message.StartsWith("Execute decision:", StringComparison.Ordinal))
        {
            entry = message.Replace("Execute decision:", "Execute:", StringComparison.Ordinal);
            return true;
        }

        if (message.StartsWith("Rule summary:", StringComparison.Ordinal) ||
            message.StartsWith("ApplyPriorityRule:", StringComparison.Ordinal) ||
            message.StartsWith("Lazy scan requires metric:", StringComparison.Ordinal))
        {
            entry = message;
            return true;
        }

        return false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width, Height);
        RaceTheme.FillRoundedRect(g, rect, RaceTheme.Panel, 8);
        using (var path = RaceTheme.BuildRoundedPath(rect, 8))
        using (var pen = new Pen(RaceTheme.Border, 1))
        {
            g.DrawPath(pen, path);
        }

        if (_entries.Count == 0)
        {
            TextRenderer.DrawText(
                g,
                "等待关键日志...",
                Font,
                new Rectangle(Pad, 0, Math.Max(0, Width - Pad * 2), Height),
                RaceTheme.TextDim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            return;
        }

        int y = Pad;
        foreach (string entry in _entries)
        {
            var lineRect = new Rectangle(Pad, y, Math.Max(0, Width - Pad * 2), LineHeight);
            TextRenderer.DrawText(
                g,
                entry,
                Font,
                lineRect,
                ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            y += LineHeight;
        }
    }

    private static string StripTimestamp(string line)
    {
        int end = line.IndexOf("] ", StringComparison.Ordinal);
        return end >= 0 && end + 2 < line.Length
            ? line[(end + 2)..]
            : line;
    }
}
