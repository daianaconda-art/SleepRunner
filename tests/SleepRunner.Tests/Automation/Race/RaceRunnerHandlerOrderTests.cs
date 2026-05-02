using System.Reflection;
using SleepRunner.Automation.Race;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class RaceRunnerHandlerOrderTests
{
    [Fact]
    public void Default_handler_order_checks_training_before_late_menu_and_commission_paths()
    {
        var runner = new RaceRunner();
        var handlers = GetHandlers(runner);

        int trainingIndex = handlers.FindIndex(static h => h.GetType().Name == "TrainingSelectHandler");
        int appraiseIndex = handlers.FindIndex(static h => h.GetType().Name == "AppraiseAcceptHandler");
        int battleLeaveIndex = handlers.FindIndex(static h => h.GetType().Name == "BattleLeaveHandler");
        int commissionIndex = handlers.FindIndex(static h => h.GetType().Name == "CommissionHandler");
        int mainMenuIndex = handlers.FindIndex(static h => h.GetType().Name == "MainMenuHandler");

        Assert.True(trainingIndex >= 0, "TrainingSelectHandler should be registered.");
        Assert.True(appraiseIndex >= 0, "AppraiseAcceptHandler should be registered.");
        Assert.True(battleLeaveIndex >= 0, "BattleLeaveHandler should be registered.");
        Assert.True(commissionIndex >= 0, "CommissionHandler should be registered.");
        Assert.True(mainMenuIndex >= 0, "MainMenuHandler should be registered.");

        Assert.True(trainingIndex < appraiseIndex, "TrainingSelectHandler should run before AppraiseAcceptHandler.");
        Assert.True(trainingIndex < battleLeaveIndex, "TrainingSelectHandler should run before BattleLeaveHandler.");
        Assert.True(trainingIndex < commissionIndex, "TrainingSelectHandler should run before CommissionHandler.");
        Assert.True(trainingIndex < mainMenuIndex, "TrainingSelectHandler should run before MainMenuHandler.");
    }

    [Fact]
    public void Default_handler_order_checks_fast_forward_settings_before_generic_event_handler()
    {
        var runner = new RaceRunner();
        var handlers = GetHandlers(runner);

        int fastForwardSettingsIndex = handlers.FindIndex(static h => h.GetType().Name == "EventFastForwardSettingsHandler");
        int eventIndex = handlers.FindIndex(static h => h.GetType().Name == "EventHandler");

        Assert.True(fastForwardSettingsIndex >= 0, "EventFastForwardSettingsHandler should be registered.");
        Assert.True(eventIndex >= 0, "EventHandler should be registered.");
        Assert.True(fastForwardSettingsIndex < eventIndex, "Event fast-forward settings should be handled before generic event selection.");
    }

    [Fact]
    public void Default_handler_order_checks_game_fast_forward_toggle_before_page_handlers()
    {
        var runner = new RaceRunner();
        var handlers = GetHandlers(runner);

        int gameFastForwardIndex = handlers.FindIndex(static h => h.GetType().Name == "GameFastForwardHandler");
        int eventIndex = handlers.FindIndex(static h => h.GetType().Name == "EventHandler");
        int trainingIndex = handlers.FindIndex(static h => h.GetType().Name == "TrainingSelectHandler");

        Assert.True(gameFastForwardIndex >= 0, "GameFastForwardHandler should be registered.");
        Assert.True(eventIndex >= 0, "EventHandler should be registered.");
        Assert.True(trainingIndex >= 0, "TrainingSelectHandler should be registered.");
        Assert.True(gameFastForwardIndex < eventIndex, "Game fast-forward toggle should run before generic event selection.");
        Assert.True(gameFastForwardIndex < trainingIndex, "Game fast-forward toggle should run before training selection.");
    }

    private static List<IRaceHandler> GetHandlers(RaceRunner runner)
    {
        var field = typeof(RaceRunner).GetField("_handlers", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new Xunit.Sdk.XunitException("RaceRunner._handlers field was not found.");

        return ((IEnumerable<IRaceHandler>)field.GetValue(runner)!).ToList();
    }
}
