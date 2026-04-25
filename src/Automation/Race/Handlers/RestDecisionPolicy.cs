namespace SleepRunner.Automation.Race.Handlers;

internal static class RestDecisionPolicy
{
    internal static bool TryChooseOption(int? money, out int option)
    {
        option = 0;
        if (!money.HasValue || money.Value < 0)
            return false;

        if (money.Value >= 60)
        {
            option = 3;
            return true;
        }

        if (money.Value >= 30)
        {
            option = 2;
            return true;
        }

        option = 1;
        return true;
    }
}
