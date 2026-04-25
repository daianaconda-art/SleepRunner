using System.Drawing;
using System.Windows.Forms;
using SleepRunner.Automation;
using SleepRunner.Automation.Race;
using SleepRunner.Forms;
using Xunit;

namespace SleepRunner.Tests.Forms;

public class WarmUiShellTests
{
    [Fact]
    public void RaceMainWindow_uses_warm_shell_defaults()
    {
        WinFormsTestHost.Run(() =>
        {
            using var window = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.RaceMainWindow",
                new StubRaceController());

            Assert.InRange(window.ClientSize.Width, 404, 420);
            Assert.True(window.ClientSize.Height >= 700, $"Expected height >= 700, got {window.ClientSize.Height}.");
            Assert.Equal(Color.FromArgb(247, 244, 239), window.BackColor);
            Assert.True(window.MinimumSize.Width >= 392, $"Expected minimum width >= 392, got {window.MinimumSize.Width}.");
        });
    }

    [Fact]
    public void RaceMainWindow_places_status_and_actions_inside_shared_hero_card()
    {
        WinFormsTestHost.Run(() =>
        {
            using var window = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.RaceMainWindow",
                new StubRaceController());

            window.ClientSize = new Size(window.MinimumSize.Width, window.MinimumSize.Height);

            Rectangle heroCardBounds = WinFormsTestHost.ReadPrivateField<Rectangle>(window, "_heroCardBounds");
            Control titleBar = WinFormsTestHost.ReadPrivateField<Control>(window, "_titleBar");
            Control heroHost = WinFormsTestHost.ReadPrivateField<Control>(window, "_heroHost");
            Control status = WinFormsTestHost.ReadPrivateField<Control>(window, "_status");
            Control actions = WinFormsTestHost.ReadPrivateField<Control>(window, "_actions");
            Control sectionTuning = WinFormsTestHost.ReadPrivateField<Control>(window, "_sectionTuning");

            Assert.False(heroCardBounds.IsEmpty);
            Assert.Equal(window.MinimumSize.Width, heroCardBounds.Width + 28);
            Assert.Equal(titleBar.Bottom + 14, heroCardBounds.Top);
            Assert.Same(heroHost, status.Parent);
            Assert.Same(heroHost, actions.Parent);
            Assert.Same(window, heroHost.Parent);
            Assert.True(heroCardBounds.Contains(heroHost.Bounds), $"Hero card {heroCardBounds} did not contain hero host {heroHost.Bounds}.");
            Assert.True(heroHost.ClientRectangle.Contains(status.Bounds), $"Hero host {heroHost.ClientRectangle} did not contain status {status.Bounds}.");
            Assert.True(heroHost.ClientRectangle.Contains(actions.Bounds), $"Hero host {heroHost.ClientRectangle} did not contain actions {actions.Bounds}.");
            Assert.True(Math.Abs(status.Top - actions.Top) <= 12, $"Expected status/actions top alignment within 12px, got status={status.Top}, actions={actions.Top}.");
            Assert.True(status.Right + 12 <= actions.Left, $"Expected at least 12px between status and actions, got status.Right={status.Right}, actions.Left={actions.Left}.");
            Assert.Equal(heroCardBounds.Bottom + 18, sectionTuning.Top);
        });
    }

    private sealed class StubRaceController : IRaceController
    {
        public RaceState State => RaceState.Idle;

        public event Action<RaceState>? StateChanged
        {
            add { }
            remove { }
        }

        public event Action<string>? ActivityChanged
        {
            add { }
            remove { }
        }

        public void Dispose()
        {
        }

        public void Pause()
        {
        }

        public void Resume()
        {
        }

        public void Start()
        {
        }

        public Task StopAsync() => Task.CompletedTask;
    }
}
