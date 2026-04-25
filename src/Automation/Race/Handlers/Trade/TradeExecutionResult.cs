namespace SleepRunner.Automation.Race.Handlers.Trade;

public enum TradeExecutionResult
{
    Purchased,
    NoPurchase,
    ManualRequired,
}

internal static class TradeExecutionResultPolicy
{
    internal static bool ShouldExitTrade(TradeExecutionResult result)
    {
        return result != TradeExecutionResult.ManualRequired;
    }
}
