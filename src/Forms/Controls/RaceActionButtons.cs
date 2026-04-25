using System.Drawing;
using System.Windows.Forms;
using SleepRunner.Automation;
using SleepRunner.Forms;

namespace SleepRunner.Forms.Controls;

internal sealed class RaceActionButtons : Control
{
    private RoundedButton? _btnStart;
    private RoundedButton? _btnStop;

    private const int Gap = 10;
    public const int RowHeight = 46;

    public event Action? StartClicked;
    public event Action? StopClicked;

    public RaceActionButtons()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint
                 | ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw
                 | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = RaceTheme.Panel;

        var btnStart = new RoundedButton
        {
            Text = UiText.Actions.Start,
            Variant = RoundedButton.ButtonVariant.Primary,
            Icon = RoundedButton.LeadingIcon.Play,
            AccentColor = RaceTheme.Accent,
            ForeColor = Color.White,
            Font = RaceTheme.BoldFont(11.5F),
            Height = RowHeight,
        };
        btnStart.Click += (_, _) => StartClicked?.Invoke();

        var btnStop = new RoundedButton
        {
            Text = UiText.Actions.Stop,
            Variant = RoundedButton.ButtonVariant.Secondary,
            Icon = RoundedButton.LeadingIcon.Stop,
            AccentColor = RaceTheme.Accent,
            ForeColor = RaceTheme.TextSecondary,
            Font = RaceTheme.BoldFont(11.5F),
            Height = RowHeight,
        };
        btnStop.Click += (_, _) => StopClicked?.Invoke();

        _btnStart = btnStart;
        _btnStop = btnStop;
        Controls.Add(btnStart);
        Controls.Add(btnStop);

        Height = RowHeight;
        LayoutButtons();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutButtons();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using var brush = new SolidBrush(BackColor);
        e.Graphics.FillRectangle(brush, ClientRectangle);
        base.OnPaint(e);
    }

    private void LayoutButtons()
    {
        var start = _btnStart;
        var stop = _btnStop;
        if (start is null || stop is null || Width <= 0) return;

        int total = Math.Max(0, Width - Gap);
        int wStart = (int)(total * 0.6);
        int wStop = total - wStart;
        start.SetBounds(0, 0, wStart, RowHeight);
        stop.SetBounds(wStart + Gap, 0, wStop, RowHeight);
    }

    public void ApplyState(RaceState state)
    {
        if (_btnStart is null || _btnStop is null) return;

        switch (state)
        {
            case RaceState.Idle:
            case RaceState.Stopped:
                _btnStart.Enabled = true;
                _btnStop.Enabled = false;
                break;
            case RaceState.Running:
            case RaceState.Paused:
                _btnStart.Enabled = false;
                _btnStop.Enabled = true;
                break;
            case RaceState.Stopping:
                _btnStart.Enabled = false;
                _btnStop.Enabled = false;
                break;
        }
    }

    public void DisableStop()
    {
        if (_btnStop is not null) _btnStop.Enabled = false;
    }
}
