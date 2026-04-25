namespace SleepRunner.Automation.Race.Handlers.Trade;

internal static class TradeExitEscPolicy
{
    internal static bool ShouldSendEsc(int attempt, bool tradeScreen, bool stageMenuReady)
    {
        if (attempt <= 0)
            throw new ArgumentOutOfRangeException(nameof(attempt));

        if (stageMenuReady)
            return false;

        if (attempt == 1)
            return true;

        return tradeScreen;
    }
}
