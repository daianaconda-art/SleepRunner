using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;
using SleepRunner.Forms;

namespace SleepRunner.Forms.Controls;

internal sealed class ConfigRow : Control
{
    private readonly Label _label;
    private readonly Label _hint;
    private Control? _editor;
    private bool _hover;

    public const int RowHeight = 60;
    private const int RowPaddingX = 16;
    private const int EditorRightMargin = 14;

    public ConfigRow(string label, string hint)
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint
                 | ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.SupportsTransparentBackColor
                 | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        Height = RowHeight;

        _label = new Label
        {
            Text = label,
            Font = RaceTheme.BoldFont(10.5F),
            ForeColor = RaceTheme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(RowPaddingX, 8),
            Size = new Size(200, 18),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        Controls.Add(_label);

        _hint = new Label
        {
            Text = hint,
            Font = RaceTheme.CaptionFont(),
            ForeColor = RaceTheme.TextDim,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(RowPaddingX, 30),
            Size = new Size(220, 16),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        Controls.Add(_hint);

        foreach (var control in new Control[] { _label, _hint })
        {
            control.MouseEnter += (_, _) => SetHover(true);
            control.MouseLeave += (_, _) => UpdateHoverFromCursor();
            control.Click += (_, _) => _editor?.Focus();
        }
    }

    public void SetEditor(Control editor)
    {
        if (_editor != null)
        {
            _editor.SizeChanged -= OnEditorSizeChanged;
            Controls.Remove(_editor);
        }

        _editor = editor;
        Controls.Add(editor);
        editor.MouseEnter += (_, _) => SetHover(true);
        editor.MouseLeave += (_, _) => UpdateHoverFromCursor();
        editor.SizeChanged += OnEditorSizeChanged;
        LayoutEditor();
    }

    private void OnEditorSizeChanged(object? s, EventArgs e) => LayoutEditor();

    private void LayoutEditor()
    {
        if (_editor == null) return;

        int x = Width - _editor.Width - EditorRightMargin;
        int y = (Height - _editor.Height) / 2;
        _editor.Location = new Point(x, y);

        int textRight = x - 10;
        _label.Size = new Size(Math.Max(60, textRight - RowPaddingX), _label.Height);
        _hint.Size = new Size(Math.Max(60, textRight - RowPaddingX), _hint.Height);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutEditor();
    }

    protected override void OnMouseEnter(EventArgs e) { SetHover(true); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { UpdateHoverFromCursor(); base.OnMouseLeave(e); }

    private void UpdateHoverFromCursor()
    {
        bool inside = ClientRectangle.Contains(PointToClient(Cursor.Position));
        SetHover(inside);
    }

    private void SetHover(bool h)
    {
        if (_hover == h) return;
        _hover = h;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!_hover) return;

        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        var rect = new Rectangle(4, 2, Width - 8, Height - 4);
        RaceTheme.FillRoundedRect(g, rect, Color.FromArgb(20, 255, 255, 255), 5);
    }
}
