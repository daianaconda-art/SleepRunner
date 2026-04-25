using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

namespace SleepRunner.Forms.Controls;

/// <summary>
/// 紧凑自绘数值步进器：[ −  value unit  + ]
///
/// 设计要点：
///   - 抛弃原生 NumericUpDown 的灰色箭头，整体外观与卡片统一
///   - 中间数字使用等宽字体居中，可双击进入编辑态；Enter / 失焦提交
///   - 左右两侧 24px 命中区域为 "−" / "+"，按下即步进；支持鼠标滚轮快速调节
///   - 通过 Suffix 直接画出 "%"、"x" 等单位，避免在外部再叠一个 Label
/// </summary>
internal sealed class NumericStepper : Control
{
    private decimal _value;
    private decimal _minimum;
    private decimal _maximum = 100m;
    private decimal _increment = 1m;
    private int _decimals;
    private string _suffix = string.Empty;

    private bool _hoverMinus;
    private bool _hoverPlus;
    private bool _pressedMinus;
    private bool _pressedPlus;

    private TextBox? _editor;

    /// <summary>值变化时触发（包含步进、滚轮、文本提交）</summary>
    public event Action? ValueChanged;

    public decimal Minimum
    {
        get => _minimum;
        set { _minimum = value; Value = Clamp(_value); }
    }

    public decimal Maximum
    {
        get => _maximum;
        set { _maximum = value; Value = Clamp(_value); }
    }

    public decimal Increment
    {
        get => _increment;
        set => _increment = value > 0 ? value : 1m;
    }

    public int DecimalPlaces
    {
        get => _decimals;
        set { _decimals = Math.Clamp(value, 0, 4); Invalidate(); }
    }

    /// <summary>显示在数字后面的单位字符（如 "%"、"x"），不参与数值</summary>
    public string Suffix
    {
        get => _suffix;
        set { _suffix = value ?? string.Empty; Invalidate(); }
    }

    public decimal Value
    {
        get => _value;
        set
        {
            decimal v = Clamp(value);
            if (_value == v) return;
            _value = v;
            Invalidate();
            ValueChanged?.Invoke();
        }
    }

    public NumericStepper()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint
                 | ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.SupportsTransparentBackColor
                 | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        ForeColor = RaceTheme.TextPrimary;
        Font = RaceTheme.NumericFont();
        Size = new Size(132, 36);
        TabStop = true;
    }

    private decimal Clamp(decimal v) => Math.Max(_minimum, Math.Min(_maximum, v));

    private Rectangle MinusRect => new(0, 0, ButtonWidth, Height);
    private Rectangle PlusRect => new(Width - ButtonWidth, 0, ButtonWidth, Height);
    private int ButtonWidth => 32;

    /// <summary>步进一次（带方向）；负号区为 -1，正号区为 +1</summary>
    private void Step(int direction)
    {
        if (direction == 0) return;
        Value = Clamp(_value + _increment * direction);
    }

    // ---------- 鼠标 ----------
    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool m = MinusRect.Contains(e.Location);
        bool p = PlusRect.Contains(e.Location);
        if (m != _hoverMinus || p != _hoverPlus)
        {
            _hoverMinus = m;
            _hoverPlus = p;
            Invalidate();
        }
        Cursor = (m || p) ? Cursors.Hand : Cursors.IBeam;
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hoverMinus = _hoverPlus = false;
        _pressedMinus = _pressedPlus = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (MinusRect.Contains(e.Location)) { _pressedMinus = true; Step(-1); Invalidate(); }
        else if (PlusRect.Contains(e.Location)) { _pressedPlus = true; Step(+1); Invalidate(); }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressedMinus = _pressedPlus = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        if (!MinusRect.Contains(e.Location) && !PlusRect.Contains(e.Location))
            BeginEdit();
        base.OnMouseDoubleClick(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        Step(e.Delta > 0 ? +1 : -1);
        base.OnMouseWheel(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Up:
            case Keys.Right:
                Step(+1); e.Handled = true; break;
            case Keys.Down:
            case Keys.Left:
                Step(-1); e.Handled = true; break;
            case Keys.Enter:
            case Keys.F2:
                BeginEdit(); e.Handled = true; break;
        }
        base.OnKeyDown(e);
    }

    // ---------- 编辑态：嵌入临时 TextBox ----------
    private void BeginEdit()
    {
        if (_editor != null) return;

        var inner = new Rectangle(ButtonWidth + 2, 5, Width - ButtonWidth * 2 - 4, Height - 10);
        _editor = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = RaceTheme.SurfaceSunken,
            ForeColor = RaceTheme.TextPrimary,
            Font = RaceTheme.NumericFont(),
            TextAlign = HorizontalAlignment.Center,
            Text = _value.ToString("F" + _decimals, CultureInfo.InvariantCulture),
            Bounds = inner,
        };
        _editor.SelectAll();
        _editor.KeyDown += (_, ev) =>
        {
            if (ev.KeyCode == Keys.Enter) { CommitEdit(); ev.SuppressKeyPress = true; }
            else if (ev.KeyCode == Keys.Escape) { CancelEdit(); ev.SuppressKeyPress = true; }
        };
        _editor.LostFocus += (_, _) => CommitEdit();
        Controls.Add(_editor);
        _editor.Focus();
    }

    private void CommitEdit()
    {
        if (_editor == null) return;
        if (decimal.TryParse(_editor.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            || decimal.TryParse(_editor.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out v))
        {
            Value = Clamp(v);
        }
        DisposeEditor();
    }

    private void CancelEdit() => DisposeEditor();

    private void DisposeEditor()
    {
        if (_editor == null) return;
        var ed = _editor;
        _editor = null;
        Controls.Remove(ed);
        ed.Dispose();
        Focus();
        Invalidate();
    }

    // ---------- 绘制 ----------
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // 整体凹陷面
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = RaceTheme.BuildRoundedPath(rect, Height / 2))
        using (var brush = new SolidBrush(RaceTheme.Panel))
            g.FillPath(brush, path);

        // 边框（聚焦时高亮）
        var borderColor = Focused ? RaceTheme.Accent : RaceTheme.Border;
        using (var path = RaceTheme.BuildRoundedPath(rect, Height / 2))
        using (var pen = new Pen(borderColor, 1))
            g.DrawPath(pen, path);

        // 左右按钮的 hover/pressed 高亮（圆角左右半边）
        DrawSideHighlight(g, MinusRect, _pressedMinus, _hoverMinus, leftSide: true);
        DrawSideHighlight(g, PlusRect, _pressedPlus, _hoverPlus, leftSide: false);

        // 绘制 "−" 与 "+"
        DrawGlyph(g, MinusRect, isMinus: true);
        DrawGlyph(g, PlusRect, isMinus: false);

        // 中间值（编辑态由 TextBox 接管）
        if (_editor == null)
        {
            string text = _value.ToString("F" + _decimals, CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(_suffix)) text += _suffix;
            var inner = new Rectangle(ButtonWidth, 0, Width - ButtonWidth * 2, Height);
            TextRenderer.DrawText(g, text, Font, inner, RaceTheme.TextPrimary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    private static void DrawSideHighlight(Graphics g, Rectangle r, bool pressed, bool hover, bool leftSide)
    {
        if (!hover && !pressed) return;
        Color c = pressed ? Color.FromArgb(60, RaceTheme.Accent) : Color.FromArgb(28, 255, 255, 255);
        using var brush = new SolidBrush(c);
        using var path = new GraphicsPath();
        int radius = 6;
        var rect = new Rectangle(r.X, r.Y, r.Width, r.Height - 1);
        if (leftSide)
        {
            path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
            path.AddLine(rect.X + radius, rect.Y, rect.Right, rect.Y);
            path.AddLine(rect.Right, rect.Y, rect.Right, rect.Bottom);
            path.AddLine(rect.Right, rect.Bottom, rect.X + radius, rect.Bottom);
            path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
        }
        else
        {
            path.AddLine(rect.X, rect.Y, rect.Right - radius, rect.Y);
            path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddLine(rect.Right - radius, rect.Bottom, rect.X, rect.Bottom);
            path.CloseFigure();
        }
        g.FillPath(brush, path);
    }

    private static void DrawGlyph(Graphics g, Rectangle area, bool isMinus)
    {
        var center = new Point(area.X + area.Width / 2, area.Y + area.Height / 2);
        const int half = 4;
        using var pen = new Pen(RaceTheme.TextSecondary, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, center.X - half, center.Y, center.X + half, center.Y);
        if (!isMinus)
            g.DrawLine(pen, center.X, center.Y - half, center.X, center.Y + half);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Up:
            case Keys.Down:
            case Keys.Left:
            case Keys.Right:
                return true;
        }
        return base.IsInputKey(keyData);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
}
