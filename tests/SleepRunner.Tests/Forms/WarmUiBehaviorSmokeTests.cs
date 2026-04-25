using System.Linq;
using System.Windows.Forms;
using SleepRunner.Automation;
using SleepRunner.Automation.Race.Policy;
using SleepRunner.Automation.Race.Policy.Training;
using SleepRunner.Utils;
using Xunit;

namespace SleepRunner.Tests.Forms;

public class WarmUiBehaviorSmokeTests
{
    [Fact]
    public void RaceActionButtons_apply_state_toggles_buttons_for_idle_and_stopped()
    {
        WinFormsTestHost.Run(() =>
        {
            using var buttons = (Control)WinFormsTestHost.CreateInternal("SleepRunner.Forms.Controls.RaceActionButtons");
            using var host = new Form();
            host.Controls.Add(buttons);

            var start = WinFormsTestHost.ReadPrivateField<Control>(buttons, "_btnStart");
            var stop = WinFormsTestHost.ReadPrivateField<Control>(buttons, "_btnStop");

            buttons.GetType().GetMethod("ApplyState")!.Invoke(buttons, new object[] { RaceState.Idle });
            AssertButtons(start, stop, startEnabled: true, stopEnabled: false);

            buttons.GetType().GetMethod("ApplyState")!.Invoke(buttons, new object[] { RaceState.Stopped });
            AssertButtons(start, stop, startEnabled: true, stopEnabled: false);
        });
    }

    [Fact]
    public void RaceActionButtons_apply_state_toggles_buttons_for_running_and_paused()
    {
        WinFormsTestHost.Run(() =>
        {
            using var buttons = (Control)WinFormsTestHost.CreateInternal("SleepRunner.Forms.Controls.RaceActionButtons");
            using var host = new Form();
            host.Controls.Add(buttons);

            var start = WinFormsTestHost.ReadPrivateField<Control>(buttons, "_btnStart");
            var stop = WinFormsTestHost.ReadPrivateField<Control>(buttons, "_btnStop");

            buttons.GetType().GetMethod("ApplyState")!.Invoke(buttons, new object[] { RaceState.Running });
            AssertButtons(start, stop, startEnabled: false, stopEnabled: true);

            buttons.GetType().GetMethod("ApplyState")!.Invoke(buttons, new object[] { RaceState.Paused });
            AssertButtons(start, stop, startEnabled: false, stopEnabled: true);
        });
    }

    [Fact]
    public void RaceActionButtons_apply_state_toggles_buttons_for_stopping()
    {
        WinFormsTestHost.Run(() =>
        {
            using var buttons = (Control)WinFormsTestHost.CreateInternal("SleepRunner.Forms.Controls.RaceActionButtons");
            using var host = new Form();
            host.Controls.Add(buttons);

            var start = WinFormsTestHost.ReadPrivateField<Control>(buttons, "_btnStart");
            var stop = WinFormsTestHost.ReadPrivateField<Control>(buttons, "_btnStop");

            buttons.GetType().GetMethod("ApplyState")!.Invoke(buttons, new object[] { RaceState.Stopping });
            AssertButtons(start, stop, startEnabled: false, stopEnabled: false);
        });
    }

    [Fact]
    public void ProfilesStrip_selection_change_raises_event_and_updates_profile_manager()
    {
        WinFormsTestHost.Run(() =>
        {
            string originalEvents = RaceProfileManager.CurrentEventsProfile;
            string originalCards = RaceProfileManager.CurrentCardsProfile;
            string originalTrade = RaceProfileManager.CurrentTradeProfile;

            try
            {
                using var strip = (Control)WinFormsTestHost.CreateInternal(
                    "SleepRunner.Forms.Controls.ProfilesStrip",
                    new UserSettings());
                using var host = new Form();
                host.Controls.Add(strip);
                host.CreateControl();
                strip.CreateControl();

                var eventsCombo = WinFormsTestHost.ReadPrivateField<ComboBox>(strip, "_cmbEvents");
                var cardsCombo = WinFormsTestHost.ReadPrivateField<ComboBox>(strip, "_cmbCards");
                var tradeCombo = WinFormsTestHost.ReadPrivateField<ComboBox>(strip, "_cmbTrade");
                eventsCombo.CreateControl();
                cardsCombo.CreateControl();
                tradeCombo.CreateControl();

                SetComboChoices(eventsCombo, "default", "events-alt");
                SetComboChoices(cardsCombo, "default", "cards-alt");
                SetComboChoices(tradeCombo, "default", "trade-alt");

                int changedCount = 0;
                (string events, string cards, string trade) observed = default;
                strip.GetType().GetEvent("ProfilesChanged")!.AddEventHandler(strip, new Action<string, string, string>((events, cards, trade) =>
                {
                    changedCount++;
                    observed = (events, cards, trade);
                }));

                eventsCombo.SelectedIndex = 1;

                Assert.Equal(1, changedCount);
                Assert.Equal(("events-alt", "default", "default"), observed);
                Assert.Equal("events-alt", RaceProfileManager.CurrentEventsProfile);
                Assert.Equal("default", RaceProfileManager.CurrentCardsProfile);
                Assert.Equal("default", RaceProfileManager.CurrentTradeProfile);
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
    public void TrainingRulesStrip_selection_change_raises_profile_changed_when_selection_changes()
    {
        WinFormsTestHost.Run(() =>
        {
            string originalProfile = TrainingRuleProfileManager.CurrentProfile;

            try
            {
                using var strip = (Control)WinFormsTestHost.CreateInternal(
                    "SleepRunner.Forms.Controls.TrainingRulesStrip",
                    "default");
                using var host = new Form();
                host.Controls.Add(strip);
                host.CreateControl();
                strip.CreateControl();

                var combo = WinFormsTestHost.ReadPrivateField<ComboBox>(strip, "_cmbProfile");
                combo.CreateControl();
                SetComboChoices(combo, "default", "training-alt");

                int changedCount = 0;
                string? observed = null;
                strip.GetType().GetEvent("ProfileChanged")!.AddEventHandler(strip, new Action<string>(profile =>
                {
                    changedCount++;
                    observed = profile;
                }));

                combo.SelectedIndex = 1;

                Assert.Equal(1, changedCount);
                Assert.Equal("training-alt", observed);
            }
            finally
            {
                TrainingRuleProfileManager.SetCurrentProfile(originalProfile);
            }
        });
    }

    [Fact]
    public void FilesStrip_refresh_button_states_reenables_disabled_buttons()
    {
        WinFormsTestHost.Run(() =>
        {
            using var strip = (Control)WinFormsTestHost.CreateInternal("SleepRunner.Forms.Controls.FilesStrip");
            using var host = new Form();
            host.Controls.Add(strip);
            host.CreateControl();
            strip.CreateControl();

            var buttons = strip.Controls.Cast<Control>().ToArray();
            Assert.Equal(3, buttons.Length);
            Assert.All(buttons, button => button.Enabled = false);

            strip.GetType().GetMethod("RefreshButtonStates")!.Invoke(strip, null);

            Assert.All(buttons, button => Assert.True(button.Enabled));
        });
    }

    private static void SetComboChoices(ComboBox combo, params string[] items)
    {
        combo.Items.Clear();
        foreach (string item in items)
        {
            combo.Items.Add(item);
        }

        combo.SelectedIndex = 0;
    }

    private static void AssertButtons(Control start, Control stop, bool startEnabled, bool stopEnabled)
    {
        Assert.Equal(startEnabled, start.Enabled);
        Assert.Equal(stopEnabled, stop.Enabled);
    }
}
