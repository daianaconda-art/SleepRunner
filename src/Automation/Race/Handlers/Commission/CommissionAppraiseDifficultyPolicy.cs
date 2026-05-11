namespace SleepRunner.Automation.Race.Handlers.Commission;

internal static class CommissionAppraiseDifficultyPolicy
{
    public static int ResolveListOptionIndex(AppraiseDifficultyMode mode)
    {
        return mode == AppraiseDifficultyMode.Normal ? 2 : 3;
    }

    public static bool ShouldStartDifficultyBasedPopup(bool isRedDifficult)
    {
        return isRedDifficult;
    }

    public static bool ShouldMarkRedCommissionCardReward(
        bool isBattleCommissionPopup,
        bool isRedDifficult)
    {
        return isBattleCommissionPopup && isRedDifficult;
    }
}
