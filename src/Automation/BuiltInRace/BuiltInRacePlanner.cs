namespace SleepRunner.Automation.BuiltInRace;

public static class BuiltInRacePlanner
{
    public static BuiltInRaceAction? Decide(BuiltInRaceScreenSnapshot snapshot)
    {
        string title = Normalize(snapshot.JourneyTitleText);
        string bottomRight = Normalize(snapshot.BottomRightText);
        string bottomJourney = Normalize(snapshot.BottomJourneyText);
        string dialogTitle = Normalize(snapshot.DialogTitleText);
        string dialogBody = Normalize(snapshot.DialogBodyText);

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
                0.876,
                "自动旅程弹窗 -> 开始旅程");
        }

        if (HasJourneyStartTitle(title) &&
            bottomJourney.Contains("自动旅程", StringComparison.Ordinal))
        {
            return new BuiltInRaceAction(
                BuiltInRaceStep.OpenAutoJourney,
                0.695,
                0.937,
                "编队页 -> 自动旅程");
        }

        if (HasJourneyStartTitle(title) &&
            bottomRight.Contains("确认", StringComparison.Ordinal))
        {
            return new BuiltInRaceAction(
                BuiltInRaceStep.ConfirmStartingCharacter,
                0.895,
                0.938,
                "旅程起点 -> 确认");
        }

        if (HasJourneyStartTitle(title) &&
            bottomRight.Contains("选择", StringComparison.Ordinal))
        {
            return new BuiltInRaceAction(
                BuiltInRaceStep.SelectStartingCharacter,
                0.885,
                0.960,
                "旅程起点 -> 选择");
        }

        return null;
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

    private static bool HasJourneyStartTitle(string text)
    {
        return text.Contains("旅程起点", StringComparison.Ordinal);
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
