namespace SleepRunner.Automation.Race.Handlers;

internal enum GameFastForwardStartupAction
{
    None,
    Click,
    ClickAndComplete,
    Complete,
}

internal sealed class GameFastForwardStartupState
{
    public bool IsComplete { get; private set; }

    public GameFastForwardStartupAction Observe(GameFastForwardState state)
    {
        if (IsComplete)
            return GameFastForwardStartupAction.None;

        return state switch
        {
            GameFastForwardState.TwoSpeed => CompleteNow(),
            GameFastForwardState.OneSpeed => GameFastForwardStartupAction.ClickAndComplete,
            GameFastForwardState.OffGray => GameFastForwardStartupAction.Click,
            _ => GameFastForwardStartupAction.None,
        };
    }

    public void MarkActionExecuted(GameFastForwardStartupAction action)
    {
        if (action is GameFastForwardStartupAction.ClickAndComplete)
            IsComplete = true;
    }

    private GameFastForwardStartupAction CompleteNow()
    {
        IsComplete = true;
        return GameFastForwardStartupAction.Complete;
    }
}
