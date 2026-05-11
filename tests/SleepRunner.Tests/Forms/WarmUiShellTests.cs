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

            Assert.InRange(window.ClientSize.Width, 502, 520);
            Assert.True(window.ClientSize.Height >= 700, $"Expected height >= 700, got {window.ClientSize.Height}.");
            Assert.Equal(Color.FromArgb(247, 244, 239), window.BackColor);
            Assert.True(window.MinimumSize.Width >= 500, $"Expected minimum width >= 500, got {window.MinimumSize.Width}.");
        });
    }

    [Fact]
    public void RaceMainWindow_uses_packaged_sleep_runner_icon()
    {
        WinFormsTestHost.Run(() =>
        {
            using var iconStream = typeof(RaceMainWindow).Assembly.GetManifestResourceStream("SleepRunner.AppIcon.ico");
            Assert.NotNull(iconStream);

            using var expectedIcon = new Icon(iconStream!);
            using var expectedBitmap = expectedIcon.ToBitmap();
            using var window = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.RaceMainWindow",
                new StubRaceController());

            Assert.NotNull(window.Icon);
            using var actualBitmap = window.Icon!.ToBitmap();
            Assert.Equal(expectedBitmap.Size, actualBitmap.Size);
            Assert.Equal(expectedBitmap.GetPixel(expectedBitmap.Width / 2, expectedBitmap.Height / 2),
                actualBitmap.GetPixel(actualBitmap.Width / 2, actualBitmap.Height / 2));
        });
    }

    [Fact]
    public void RaceMainWindow_switches_to_running_icon_for_active_states()
    {
        WinFormsTestHost.Run(() =>
        {
            using var iconStream = typeof(RaceMainWindow).Assembly.GetManifestResourceStream("SleepRunner.RunningAppIcon.ico");
            Assert.NotNull(iconStream);

            using var expectedIcon = new Icon(iconStream!);
            using var expectedBitmap = expectedIcon.ToBitmap();
            using var window = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.RaceMainWindow",
                new StubRaceController());

            WinFormsTestHost.Invoke(window, "ApplyState", RaceState.Running);

            Assert.NotNull(window.Icon);
            using var actualBitmap = window.Icon!.ToBitmap();
            Assert.Equal(expectedBitmap.Size, actualBitmap.Size);
            int sampleX = expectedBitmap.Width * 5 / 6;
            int sampleY = expectedBitmap.Height / 6;
            Assert.Equal(expectedBitmap.GetPixel(sampleX, sampleY), actualBitmap.GetPixel(sampleX, sampleY));
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
            Control automationPage = WinFormsTestHost.ReadPrivateField<Control>(window, "_automationPage");
            Control heroHost = WinFormsTestHost.ReadPrivateField<Control>(window, "_heroHost");
            Control status = WinFormsTestHost.ReadPrivateField<Control>(window, "_status");
            Control actions = WinFormsTestHost.ReadPrivateField<Control>(window, "_actions");
            Control sectionKeyLog = WinFormsTestHost.ReadPrivateField<Control>(window, "_sectionKeyLog");
            Control keyLog = WinFormsTestHost.ReadPrivateField<Control>(window, "_keyLog");
            Control sectionTuning = WinFormsTestHost.ReadPrivateField<Control>(window, "_sectionTuning");

            Assert.False(heroCardBounds.IsEmpty);
            Assert.Equal(automationPage.Width, heroCardBounds.Width);
            Assert.Equal(titleBar.Bottom + 14, automationPage.Top);
            Assert.Equal(0, heroCardBounds.Top);
            Assert.Same(heroHost, status.Parent);
            Assert.Same(heroHost, actions.Parent);
            Assert.Same(automationPage, heroHost.Parent);
            Assert.True(heroCardBounds.Contains(heroHost.Bounds), $"Hero card {heroCardBounds} did not contain hero host {heroHost.Bounds}.");
            Assert.True(heroHost.ClientRectangle.Contains(status.Bounds), $"Hero host {heroHost.ClientRectangle} did not contain status {status.Bounds}.");
            Assert.True(heroHost.ClientRectangle.Contains(actions.Bounds), $"Hero host {heroHost.ClientRectangle} did not contain actions {actions.Bounds}.");
            Assert.True(Math.Abs(status.Top - actions.Top) <= 12, $"Expected status/actions top alignment within 12px, got status={status.Top}, actions={actions.Top}.");
            Assert.True(status.Right + 12 <= actions.Left, $"Expected at least 12px between status and actions, got status.Right={status.Right}, actions.Left={actions.Left}.");
            Assert.Equal(heroCardBounds.Bottom + 18, sectionKeyLog.Top);
            Assert.Equal(sectionKeyLog.Bottom + 8, keyLog.Top);
            Assert.Equal(keyLog.Bottom + 14, sectionTuning.Top);
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
