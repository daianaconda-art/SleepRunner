using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SleepRunner.Automation;
using SleepRunner.Automation.Race;
using SleepRunner.Automation.Race.Policy;
using SleepRunner.Automation.Race.Policy.Training;
using SleepRunner.Forms;
using SleepRunner.Utils;
using Xunit;

namespace SleepRunner.Tests.Forms;

public class WarmUiMainWindowBehaviorTests
{
    [Fact]
    public void RaceMainWindow_start_button_click_invokes_controller_start()
    {
        WinFormsTestHost.Run(() =>
        {
            var controller = new StubRaceController();
            using var window = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.RaceMainWindow",
                controller);

            var actions = WinFormsTestHost.ReadPrivateField<Control>(window, "_actions");
            var startButton = WinFormsTestHost.ReadPrivateField<Button>(actions, "_btnStart");

            ClickButton(startButton);

            Assert.Equal(1, controller.StartCallCount);
            Assert.Equal(0, controller.StopCallCount);
        });
    }

    [Fact]
    public void RaceMainWindow_stop_button_click_invokes_controller_stop_async()
    {
        WinFormsTestHost.Run(() =>
        {
            var controller = new StubRaceController();
            using var window = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.RaceMainWindow",
                controller);

            controller.SetState(RaceState.Running);

            var actions = WinFormsTestHost.ReadPrivateField<Control>(window, "_actions");
            var stopButton = WinFormsTestHost.ReadPrivateField<Button>(actions, "_btnStop");

            ClickButton(stopButton);

            Assert.Equal(0, controller.StartCallCount);
            Assert.Equal(1, controller.StopCallCount);
            Assert.False(stopButton.Enabled);
        });
    }

    [Fact]
    public void RaceMainWindow_profile_combo_changes_update_settings_and_profile_manager()
    {
        WinFormsTestHost.Run(() =>
        {
            string originalEvents = RaceProfileManager.CurrentEventsProfile;
            string originalCards = RaceProfileManager.CurrentCardsProfile;
            string originalTrade = RaceProfileManager.CurrentTradeProfile;

            try
            {
                var controller = new StubRaceController();
                using var window = (Form)WinFormsTestHost.CreateInternal(
                    "SleepRunner.Forms.RaceMainWindow",
                    controller);

                var settings = WinFormsTestHost.ReadPrivateField<UserSettings>(window, "_settings");
                var profiles = WinFormsTestHost.ReadPrivateField<Control>(window, "_profiles");

                var eventsCombo = WinFormsTestHost.ReadPrivateField<ComboBox>(profiles, "_cmbEvents");
                var cardsCombo = WinFormsTestHost.ReadPrivateField<ComboBox>(profiles, "_cmbCards");
                var tradeCombo = WinFormsTestHost.ReadPrivateField<ComboBox>(profiles, "_cmbTrade");

                ChangeSelectionToDifferentItem(eventsCombo);
                Assert.Equal(eventsCombo.SelectedItem?.ToString(), settings.EventsProfile);
                Assert.Equal(eventsCombo.SelectedItem?.ToString(), RaceProfileManager.CurrentEventsProfile);

                ChangeSelectionToDifferentItem(cardsCombo);
                Assert.Equal(cardsCombo.SelectedItem?.ToString(), settings.CardsProfile);
                Assert.Equal(cardsCombo.SelectedItem?.ToString(), RaceProfileManager.CurrentCardsProfile);

                ChangeSelectionToDifferentItem(tradeCombo);
                Assert.Equal(tradeCombo.SelectedItem?.ToString(), settings.TradeProfile);
                Assert.Equal(tradeCombo.SelectedItem?.ToString(), RaceProfileManager.CurrentTradeProfile);
            }
            finally
            {
                RaceProfileManager.SetEventsProfile(originalEvents);
                RaceProfileManager.SetCardsProfile(originalCards);
                RaceProfileManager.SetTradeProfile(originalTrade);
            }
        });
    }

    [Fact]
    public void RaceMainWindow_training_profile_combo_change_updates_settings_and_manager()
    {
        WinFormsTestHost.Run(() =>
        {
            string originalProfile = TrainingRuleProfileManager.CurrentProfile;

            try
            {
                var controller = new StubRaceController();
                using var window = (Form)WinFormsTestHost.CreateInternal(
                    "SleepRunner.Forms.RaceMainWindow",
                    controller);

                var settings = WinFormsTestHost.ReadPrivateField<UserSettings>(window, "_settings");
                var trainingRules = WinFormsTestHost.ReadPrivateField<Control>(window, "_trainingRules");
                var combo = WinFormsTestHost.ReadPrivateField<ComboBox>(trainingRules, "_cmbProfile");

                ChangeSelectionToDifferentItem(combo);

                Assert.Equal(combo.SelectedItem?.ToString(), settings.TrainingProfile);
                Assert.Equal(combo.SelectedItem?.ToString(), TrainingRuleProfileManager.CurrentProfile);
            }
            finally
            {
                TrainingRuleProfileManager.SetCurrentProfile(originalProfile);
            }
        });
    }

    [Fact]
    public void RaceMainWindow_pin_toggle_updates_topmost_and_button_state()
    {
        WinFormsTestHost.Run(() =>
        {
            using var window = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.RaceMainWindow",
                new StubRaceController());

            var pinButton = WinFormsTestHost.ReadPrivateField<Control>(window, "_btnPin");

            bool startingTopMost = window.TopMost;
            bool startingActive = (bool)pinButton.GetType().GetProperty("Active")!.GetValue(pinButton)!;

            ClickIconButton(pinButton);

            Assert.Equal(!startingTopMost, window.TopMost);
            Assert.Equal(!startingActive, (bool)pinButton.GetType().GetProperty("Active")!.GetValue(pinButton)!);
        });
    }

    [Fact]
    public void RaceMainWindow_minimize_button_click_minimizes_window()
    {
        WinFormsTestHost.Run(() =>
        {
            using var window = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.RaceMainWindow",
                new StubRaceController());

            var minButton = WinFormsTestHost.ReadPrivateField<Control>(window, "_btnMin");

            ClickIconButton(minButton);

            Assert.Equal(FormWindowState.Minimized, window.WindowState);
        });
    }

    private static void ChangeSelectionToDifferentItem(ComboBox combo)
    {
        string current = combo.SelectedItem?.ToString() ?? string.Empty;
        int nextIndex = combo.Items.Cast<object>()
            .Select((item, index) => new { Text = item?.ToString() ?? string.Empty, Index = index })
            .First(entry => !string.Equals(entry.Text, current, StringComparison.OrdinalIgnoreCase))
            .Index;

        combo.SelectedIndex = nextIndex;
    }

    private static void ClickIconButton(Control button) =>
        WinFormsTestHost.Invoke(button, "OnClick", EventArgs.Empty);

    private static void ClickButton(Button button) =>
        WinFormsTestHost.Invoke(button, "OnClick", EventArgs.Empty);

    private sealed class StubRaceController : IRaceController
    {
        private RaceState _state = RaceState.Idle;
        private Action<RaceState>? _stateChanged;
        private Action<string>? _activityChanged;

        public int StartCallCount { get; private set; }
        public int StopCallCount { get; private set; }

        public RaceState State => _state;

        public event Action<RaceState>? StateChanged
        {
            add => _stateChanged += value;
            remove => _stateChanged -= value;
        }

        public event Action<string>? ActivityChanged
        {
            add => _activityChanged += value;
            remove => _activityChanged -= value;
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
            StartCallCount++;
        }

        public Task StopAsync()
        {
            StopCallCount++;
            return Task.CompletedTask;
        }

        public void SetState(RaceState state)
        {
            _state = state;
            _stateChanged?.Invoke(state);
        }
    }
}
