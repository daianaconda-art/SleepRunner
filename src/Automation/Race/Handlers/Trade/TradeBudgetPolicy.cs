namespace SleepRunner.Automation.Race.Handlers.Trade;

internal static class TradeBudgetPolicy
{
    internal static int ResolveExecutionBudget(int detectedBudget)
    {
        return detectedBudget > 0 ? detectedBudget : int.MaxValue;
    }

    internal static bool TryResolveBudget(int detectedBudget, out int budget)
    {
        budget = 0;
        if (detectedBudget <= 0)
            return false;

        budget = detectedBudget;
        return true;
    }
}
