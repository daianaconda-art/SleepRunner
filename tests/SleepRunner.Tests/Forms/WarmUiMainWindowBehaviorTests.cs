using System.Drawing;
using System.Linq;
using System.Reflection;
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
    private const int WmHotkey = 0x0312;
    private const int AutomationToggleHotkeyId = 0x5151;
    private const int ModAlt = 0x0001;
    private const int VkQ = 0x51;
    private const int WmSyskeydown = 0x0104;
    private const int WmSyskeyup = 0x0105;
    private const int LlkAltDown = 0x20;

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
    public void RaceMainWindow_key_log_appends_filtered_training_entries()
    {
        WinFormsTestHost.Run(() =>
        {
            using var window = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.RaceMainWindow",
                new StubRaceController());

            var keyLog = WinFormsTestHost.ReadPrivateField<Control>(window, "_keyLog");

            Logger.Log("[Race:TrainingSelect] Slot 0: y=0.130 satMean=0.0 valMean=0.0 => empty (none)");
            Logger.Log("[Race:TrainingSelect] Full scan snapshot: icons=[1,1,1,2,5], fails=[0,3,0,0,0], strength=373, stamina=326");
            Logger.Log("[Race:TrainingSelect] Rule evaluation: profile=default, matched=guard_icons_3, action=TrainGuard, builtinDefault=False");

            IReadOnlyList<string> entries = ReadKeyLogEntries(keyLog);

            Assert.DoesNotContain(entries, entry => entry.Contains("Slot 0", StringComparison.Ordinal));
            Assert.Contains(entries, entry => entry.Contains("icons=[1,1,1,2,5]", StringComparison.Ordinal));
            Assert.Contains(entries, entry => entry.Contains("TrainGuard", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void RaceMainWindow_places_key_log_between_hero_and_tuning_sections()
    {
        WinFormsTestHost.Run(() =>
        {
            using var window = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.RaceMainWindow",
                new StubRaceController());

            var keyLog = WinFormsTestHost.ReadPrivateField<Control>(window, "_keyLog");
            var sectionKeyLog = WinFormsTestHost.ReadPrivateField<Control>(window, "_sectionKeyLog");
            var sectionTuning = WinFormsTestHost.ReadPrivateField<Control>(window, "_sectionTuning");
            var automationPage = WinFormsTestHost.ReadPrivateField<Control>(window, "_automationPage");
            Rectangle heroCardBounds = WinFormsTestHost.ReadPrivateField<Rectangle>(window, "_heroCardBounds");

            Assert.Same(automationPage, keyLog.Parent);
            Assert.Same(automationPage, sectionKeyLog.Parent);
            Assert.True(sectionKeyLog.Top > heroCardBounds.Bottom, $"Expected key log header below hero card, got header={sectionKeyLog.Top}, hero={heroCardBounds.Bottom}.");
            Assert.True(keyLog.Top > sectionKeyLog.Top, $"Expected key log card below its header, got card={keyLog.Top}, header={sectionKeyLog.Top}.");
            Assert.True(sectionTuning.Top > keyLog.Bottom, $"Expected tuning section below key log, got tuning={sectionTuning.Top}, keyLog.Bottom={keyLog.Bottom}.");
            Assert.InRange(keyLog.Height, 88, 132);
        });
    }

    [Fact]
    public void RaceMainWindow_uses_left_tab_navigation_with_automation_page_selected()
    {
        WinFormsTestHost.Run(() =>
        {
            using var window = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.RaceMainWindow",
                new StubRaceController(),
                new StubRaceController());

            var pageNav = WinFormsTestHost.ReadPrivateField<Control>(window, "_pageNav");
            var automationPage = WinFormsTestHost.ReadPrivateField<Control>(window, "_automationPage");
            var builtInPage = WinFormsTestHost.ReadPrivateField<Control>(window, "_builtInPage");
            var automationButton = WinFormsTestHost.ReadPrivateField<Control>(window, "_btnAutomationPage");
            var builtInButton = WinFormsTestHost.ReadPrivateField<Control>(window, "_btnBuiltInPage");
            var heroHost = WinFormsTestHost.ReadPrivateField<Control>(window, "_heroHost");
            var sectionTuning = WinFormsTestHost.ReadPrivateField<Control>(window, "_sectionTuning");

            Assert.Same(window, pageNav.Parent);
            Assert.Same(window, automationPage.Parent);
            Assert.Same(window, builtInPage.Parent);
            Assert.Same(automationPage, heroHost.Parent);
            Assert.Same(automationPage, sectionTuning.Parent);
            Assert.True(pageNav.Left < automationPage.Left, $"Expected navigation on the left, nav.Left={pageNav.Left}, page.Left={automationPage.Left}.");
            Assert.True(pageNav.Right <= automationPage.Left - 8, $"Expected a gap between navigation and content, nav.Right={pageNav.Right}, page.Left={automationPage.Left}.");
            Assert.True(ReadControlVisibleState(automationPage));
            Assert.False(ReadControlVisibleState(builtInPage));
            Assert.Contains("自动跑马", automationButton.Text, StringComparison.Ordinal);
            Assert.Contains("内置跑马", builtInButton.Text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void RaceMainWindow_switches_to_built_in_page_without_starting_or_stopping_runner()
    {
        WinFormsTestHost.Run(() =>
        {
            var controller = new StubRaceController();
            var builtInController = new StubRaceController();
            using var window = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.RaceMainWindow",
                controller,
                builtInController);

            var automationPage = WinFormsTestHost.ReadPrivateField<Control>(window, "_automationPage");
            var builtInPage = WinFormsTestHost.ReadPrivateField<Control>(window, "_builtInPage");
            var automationButton = WinFormsTestHost.ReadPrivateField<Control>(window, "_btnAutomationPage");
            var builtInButton = WinFormsTestHost.ReadPrivateField<Control>(window, "_btnBuiltInPage");

            ClickButton((Button)builtInButton);

            Assert.False(ReadControlVisibleState(automationPage));
            Assert.True(ReadControlVisibleState(builtInPage));
            Assert.Equal(0, controller.StartCallCount);
            Assert.Equal(0, controller.StopCallCount);
            Assert.Equal(0, builtInController.StartCallCount);
            Assert.Equal(0, builtInController.StopCallCount);

            ClickButton((Button)automationButton);

            Assert.True(ReadControlVisibleState(automationPage));
            Assert.False(ReadControlVisibleState(builtInPage));
            Assert.Equal(0, controller.StartCallCount);
            Assert.Equal(0, controller.StopCallCount);
            Assert.Equal(0, builtInController.StartCallCount);
            Assert.Equal(0, builtInController.StopCallCount);
        });
    }

    [Fact]
    public void RaceMainWindow_built_in_page_start_and_stop_use_built_in_controller_only()
    {
        WinFormsTestHost.Run(() =>
        {
            var automationController = new StubRaceController();
            var builtInController = new StubRaceController();
            using var window = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.RaceMainWindow",
                automationController,
                builtInController);

            var builtInButton = WinFormsTestHost.ReadPrivateField<Control>(window, "_btnBuiltInPage");
            ClickButton((Button)builtInButton);

            var builtInActions = WinFormsTestHost.ReadPrivateField<Control>(window, "_builtInActions");
            var startButton = WinFormsTestHost.ReadPrivateField<Button>(builtInActions, "_btnStart");
            var stopButton = WinFormsTestHost.ReadPrivateField<Button>(builtInActions, "_btnStop");

            ClickButton(startButton);
            builtInController.SetState(RaceState.Running);
            ClickButton(stopButton);

            Assert.Equal(0, automationController.StartCallCount);
            Assert.Equal(0, automationController.StopCallCount);
            Assert.Equal(1, builtInController.StartCallCount);
            Assert.Equal(1, builtInController.StopCallCount);
        });
    }

    [Fact]
    public void RaceMainWindow_alt_q_hotkey_starts_when_idle()
    {
        WinFormsTestHost.Run(() =>
        {
            var controller = new StubRaceController();
            using var window = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.RaceMainWindow",
                controller);

            DispatchAltQHotkey(window);

            Assert.Equal(1, controller.StartCallCount);
            Assert.Equal(0, controller.StopCallCount);
        });
    }

    [Fact]
    public void RaceMainWindow_alt_q_hotkey_stops_when_running()
    {
        WinFormsTestHost.Run(() =>
        {
            var controller = new StubRaceController();
            using var window = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.RaceMainWindow",
                controller);

            controller.SetState(RaceState.Running);

            DispatchAltQHotkey(window);

            Assert.Equal(0, controller.StartCallCount);
            Assert.Equal(1, controller.StopCallCount);
        });
    }

    [Fact]
    public void RaceMainWindow_alt_q_hotkey_ignores_while_stopping()
    {
        WinFormsTestHost.Run(() =>
        {
            var controller = new StubRaceController();
            using var window = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.RaceMainWindow",
                controller);

            controller.SetState(RaceState.Stopping);

            DispatchAltQHotkey(window);

            Assert.Equal(0, controller.StartCallCount);
            Assert.Equal(0, controller.StopCallCount);
        });
    }

    [Fact]
    public void RaceMainWindow_keyboard_hook_fallback_handles_alt_q_when_idle()
    {
        WinFormsTestHost.Run(() =>
        {
            var controller = new StubRaceController();
            using var window = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.RaceMainWindow",
                controller);

            bool handled = HandleAutomationKeyboardHook(window, WmSyskeydown, VkQ, LlkAltDown);

            Assert.True(handled);
            Assert.Equal(1, controller.StartCallCount);
            Assert.Equal(0, controller.StopCallCount);
        });
    }

    [Fact]
    public void RaceMainWindow_keyboard_hook_fallback_ignores_repeated_alt_q_until_keyup()
    {
        WinFormsTestHost.Run(() =>
        {
            var controller = new StubRaceController();
            using var window = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.RaceMainWindow",
                controller);

            bool firstHandled = HandleAutomationKeyboardHook(window, WmSyskeydown, VkQ, LlkAltDown);
            bool repeatHandled = HandleAutomationKeyboardHook(window, WmSyskeydown, VkQ, LlkAltDown);
            HandleAutomationKeyboardHook(window, WmSyskeyup, VkQ, LlkAltDown);
            bool afterKeyUpHandled = HandleAutomationKeyboardHook(window, WmSyskeydown, VkQ, LlkAltDown);

            Assert.True(firstHandled);
            Assert.True(repeatHandled);
            Assert.True(afterKeyUpHandled);
            Assert.Equal(2, controller.StartCallCount);
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

    private static void DispatchAltQHotkey(Form window)
    {
        int hotkeyPayload = (VkQ << 16) | ModAlt;
        var message = Message.Create(window.Handle, WmHotkey, (IntPtr)AutomationToggleHotkeyId, (IntPtr)hotkeyPayload);
        WinFormsTestHost.Invoke(window, "WndProc", message);
    }

    private static bool HandleAutomationKeyboardHook(Form window, int message, int vkCode, int flags)
    {
        MethodInfo method = window.GetType().GetMethod(
            "HandleAutomationKeyboardHook",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("HandleAutomationKeyboardHook method not found.");

        return (bool)method.Invoke(window, new object[] { message, vkCode, flags })!;
    }

    private static bool ReadControlVisibleState(Control control)
    {
        MethodInfo method = typeof(Control).GetMethod(
                                "GetState",
                                BindingFlags.Instance | BindingFlags.NonPublic)
                            ?? throw new InvalidOperationException("Control.GetState method not found.");

        const int stateVisible = 0x00000002;
        return (bool)method.Invoke(control, [stateVisible])!;
    }

    private static IReadOnlyList<string> ReadKeyLogEntries(Control keyLog)
    {
        PropertyInfo property = keyLog.GetType().GetProperty(
                                    "Entries",
                                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                ?? throw new InvalidOperationException("Key log Entries property not found.");

        object? value = property.GetValue(keyLog);
        return value is IEnumerable<string> entries
            ? entries.ToArray()
            : throw new InvalidOperationException("Key log Entries property did not return strings.");
    }

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
