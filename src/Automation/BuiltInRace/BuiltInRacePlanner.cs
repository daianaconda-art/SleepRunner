namespace SleepRunner.Automation.BuiltInRace;

public static class BuiltInRacePlanner
{
    public static BuiltInRaceAction? Decide(BuiltInRaceScreenSnapshot snapshot)
    {
        string bottomRight = Normalize(snapshot.BottomRightText);
        string bottomJourney = Normalize(snapshot.BottomJourneyText);
        string dialogTitle = Normalize(snapshot.DialogTitleText);
        string dialogBody = Normalize(snapshot.DialogBodyText);

        if (IsInheritJourney(dialogTitle, dialogBody))
        {
            return new BuiltInRaceAction(
                BuiltInRaceStep.InheritJourneyContinue,
                0.500,
                0.885,
                "继承旅程 -> 点击继续");
        }

        if (IsJourneyComplete(dialogTitle, dialogBody))
        {
            return new BuiltInRaceAction(
                BuiltInRaceStep.JourneyCompleteContinue,
                0.500,
                0.920,
                "旅程完成 -> 点击继续");
        }

        if (IsJourneyEndPotential(bottomRight, bottomJourney))
        {
            return new BuiltInRaceAction(
                BuiltInRaceStep.OpenPotential,
                0.730,
                0.958,
                "旅程结束 -> 潜质");
        }

        if (IsEntryConfirmation(dialogTitle, dialogBody, bottomRight))
        {
            return new BuiltInRaceAction(
                BuiltInRaceStep.ConfirmEntry,
                0.578,
                0.689,
                "入场确认 -> 确认");
        }

        if (IsAutoJourneyDialog(dialogTitle, dialogBody, bottomJourney))
        {
            return new BuiltInRaceAction(
                BuiltInRaceStep.StartAutoJourney,
                0.503,
                0.786,
                "自动旅程弹窗 -> 开始旅程");
        }

        if (bottomJourney.Contains("自动旅程", StringComparison.Ordinal))
        {
            return new BuiltInRaceAction(
                BuiltInRaceStep.OpenAutoJourney,
                0.695,
                0.937,
                "编队页 -> 自动旅程");
        }

        return null;
    }

    public static bool ShouldStopAfterAction(BuiltInRaceStep step) =>
        step == BuiltInRaceStep.OpenPotential;

    private static bool IsJourneyComplete(string dialogTitle, string dialogBody)
    {
        string text = dialogTitle + dialogBody;
        return text.Contains("JOURNEYCOMPLETE", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("完成了旅程", StringComparison.Ordinal) ||
               text.Contains("点击以继续", StringComparison.Ordinal) ||
               (text.Contains("点击", StringComparison.Ordinal) &&
                text.Contains("继续", StringComparison.Ordinal)) ||
               (text.Contains("旅程", StringComparison.Ordinal) &&
                text.Contains("完成", StringComparison.Ordinal) &&
                text.Contains("继续", StringComparison.Ordinal));
    }

    private static bool IsInheritJourney(string dialogTitle, string dialogBody)
    {
        string text = dialogTitle + dialogBody;
        return text.Contains("继承旅程", StringComparison.Ordinal) ||
               text.Contains("旅程画下句号", StringComparison.Ordinal) ||
               (text.Contains("剩余", StringComparison.Ordinal) &&
                text.Contains("退还", StringComparison.Ordinal) &&
                text.Contains("奖励", StringComparison.Ordinal));
    }

    private static bool IsJourneyEndPotential(string bottomRight, string bottomJourney)
    {
        return bottomJourney.Contains("潜质", StringComparison.Ordinal) ||
               bottomRight.Contains("旅程结束", StringComparison.Ordinal);
    }

    private static bool IsEntryConfirmation(string dialogTitle, string dialogBody, string bottomRight)
    {
        if (dialogTitle.Contains("入场确认", StringComparison.Ordinal))
            return true;

        return dialogBody.Contains("是否要进行旅程", StringComparison.Ordinal) &&
               bottomRight.Contains("确认", StringComparison.Ordinal);
    }

    private static bool IsAutoJourneyDialog(string dialogTitle, string dialogBody, string bottomJourney)
    {
        if (dialogTitle.Contains("自动旅程", StringComparison.Ordinal) &&
            !dialogTitle.Contains("旅程起点", StringComparison.Ordinal))
            return true;

        return bottomJourney.Contains("开始旅程", StringComparison.Ordinal) ||
               (dialogBody.Contains("训练频率", StringComparison.Ordinal) &&
                dialogBody.Contains("自动旅程", StringComparison.Ordinal));
    }

    public static string Normalize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        return raw
            .Replace(" ", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace("\u3000", "")
            .Trim();
    }
}
