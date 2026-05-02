using OpenCvSharp;
using SleepRunner.Recognition;

namespace SleepRunner.Automation.Race.Handlers.Events;

internal static class EventFastForwardSettingsScreenChecks
{
    private const string EventText = "\u4e8b\u4ef6";
    private const string FastForwardText = "\u5feb\u8f6c";
    private const string FastForwardTraditionalText = "\u5feb\u8f49";
    private const string SettingsText = "\u8bbe\u5b9a";
    private const string SettingsTraditionalText = "\u8a2d\u5b9a";
    private const string AllText = "\u6240\u6709";
    private const string DecideText = "\u51b3\u5b9a";
    private const string DecideTraditionalText = "\u6c7a\u5b9a";
    private const string NoText = "\u4e0d";
    private const string OnlyText = "\u4ec5";
    private const string OnlyTraditionalText = "\u50c5";
    private const string SeenText = "\u5df2\u89c2\u8d4f";
    private const string SeenTraditionalText = "\u5df2\u89c0\u8cde";

    public static bool IsScreen(Mat screenshot, out string summary)
    {
        string titleText = ReadTitleText(screenshot);
        if (!IsFastForwardSettingsTitleText(titleText))
        {
            bool layoutOnlyHit = LooksLikeSettingsLayout(screenshot);
            if (!layoutOnlyHit)
            {
                summary = $"title='{titleText}', layout=false";
                return false;
            }
        }

        string cardText = ReadCardText(screenshot);
        string confirmText = ReadConfirmText(screenshot);
        bool layoutHit = LooksLikeSettingsLayout(screenshot);
        bool hit = ShouldAcceptSignals(layoutHit, titleText, cardText, confirmText);
        summary = $"title='{titleText}', cards='{cardText}', confirm='{confirmText}', layout={layoutHit}";
        return hit;
    }

    public static bool ShouldAcceptSignals(bool layoutHit, string titleText, string cardText, string confirmText)
    {
        return IsFastForwardSettingsText(titleText, cardText, confirmText) || layoutHit;
    }

    public static bool IsFastForwardSettingsText(string titleText, string cardText, string confirmText)
    {
        string title = Normalize(titleText);
        string cards = Normalize(cardText);
        string confirm = Normalize(confirmText);
        string all = title + cards + confirm;

        bool hasTitle = title.Contains(EventText, StringComparison.Ordinal) &&
                        ContainsFastForward(title) &&
                        ContainsSettings(title);
        bool hasAllEventsChoice = ContainsFastForward(cards) &&
                                  cards.Contains(AllText, StringComparison.Ordinal) &&
                                  cards.Contains(EventText, StringComparison.Ordinal);
        bool hasNoFastForwardChoice = cards.Contains(NoText, StringComparison.Ordinal) && ContainsFastForward(cards);
        bool hasSeenChoice = ContainsOnly(cards) && ContainsSeen(cards) && ContainsFastForward(cards);
        bool hasDecide = ContainsDecide(confirm);

        int score = 0;
        if (hasTitle) score += 8;
        if (!hasTitle && all.Contains(EventText, StringComparison.Ordinal) && ContainsFastForward(all) && ContainsSettings(all)) score += 6;
        if (hasAllEventsChoice) score += 6;
        if (hasNoFastForwardChoice) score += 2;
        if (hasSeenChoice) score += 3;
        if (hasDecide) score += 3;

        return (hasTitle && hasAllEventsChoice && (hasDecide || hasNoFastForwardChoice || hasSeenChoice)) ||
               score >= 10;
    }

    private static bool IsFastForwardSettingsTitleText(string titleText)
    {
        string title = Normalize(titleText);
        return title.Contains(EventText, StringComparison.Ordinal) &&
               ContainsFastForward(title) &&
               ContainsSettings(title);
    }

    public static bool LooksLikeSettingsLayout(Mat screenshot)
    {
        if (screenshot.Empty())
            return false;

        bool darkBackdrop =
            MeanBrightness(screenshot, 0.04, 0.10, 0.10, 0.10) < 70 &&
            MeanBrightness(screenshot, 0.86, 0.10, 0.10, 0.10) < 70 &&
            MeanBrightness(screenshot, 0.05, 0.82, 0.10, 0.10) < 70 &&
            MeanBrightness(screenshot, 0.86, 0.82, 0.10, 0.10) < 70;
        if (!darkBackdrop)
            return false;

        double panelBrightness = MeanBrightness(screenshot, 0.20, 0.27, 0.60, 0.42);
        if (panelBrightness < 185)
            return false;

        int cardBands = 0;
        if (LooksLikeChoiceBand(screenshot, 0.21, 0.58, 0.19, 0.07)) cardBands++;
        if (LooksLikeChoiceBand(screenshot, 0.41, 0.58, 0.19, 0.07)) cardBands++;
        if (LooksLikeChoiceBand(screenshot, 0.60, 0.58, 0.19, 0.07)) cardBands++;
        if (cardBands < 3)
            return false;

        int brightCardImages = 0;
        if (MeanBrightness(screenshot, 0.21, 0.38, 0.19, 0.19) > 130) brightCardImages++;
        if (MeanBrightness(screenshot, 0.41, 0.38, 0.19, 0.19) > 130) brightCardImages++;
        if (MeanBrightness(screenshot, 0.60, 0.38, 0.19, 0.19) > 130) brightCardImages++;

        return brightCardImages >= 3 &&
               LooksLikeBlueRegion(screenshot, 0.43, 0.71, 0.14, 0.07);
    }

    private static string ReadTitleText(Mat screenshot)
    {
        string raw = OcrHelper.RecognizeRegion(screenshot, 0.18, 0.23, 0.34, 0.11)
            .GetAwaiter()
            .GetResult();
        return Normalize(raw);
    }

    private static string ReadCardText(Mat screenshot)
    {
        string raw = OcrHelper.RecognizeRegion(screenshot, 0.20, 0.55, 0.60, 0.13)
            .GetAwaiter()
            .GetResult();
        return Normalize(raw);
    }

    private static string ReadConfirmText(Mat screenshot)
    {
        string raw = OcrHelper.RecognizeRegion(screenshot, 0.40, 0.68, 0.20, 0.12)
            .GetAwaiter()
            .GetResult();
        return Normalize(raw);
    }

    private static bool ContainsFastForward(string text)
    {
        return text.Contains(FastForwardText, StringComparison.Ordinal) ||
               text.Contains(FastForwardTraditionalText, StringComparison.Ordinal);
    }

    private static bool ContainsSettings(string text)
    {
        return text.Contains(SettingsText, StringComparison.Ordinal) ||
               text.Contains(SettingsTraditionalText, StringComparison.Ordinal);
    }

    private static bool ContainsDecide(string text)
    {
        return text.Contains(DecideText, StringComparison.Ordinal) ||
               text.Contains(DecideTraditionalText, StringComparison.Ordinal);
    }

    private static bool ContainsOnly(string text)
    {
        return text.Contains(OnlyText, StringComparison.Ordinal) ||
               text.Contains(OnlyTraditionalText, StringComparison.Ordinal);
    }

    private static bool ContainsSeen(string text)
    {
        return text.Contains(SeenText, StringComparison.Ordinal) ||
               text.Contains(SeenTraditionalText, StringComparison.Ordinal);
    }

    private static string Normalize(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";

        return raw
            .Replace(" ", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace("\u3000", "")
            .Trim();
    }

    private static bool LooksLikeChoiceBand(Mat screenshot, double x, double y, double w, double h)
    {
        Scalar mean = MeanColor(screenshot, x, y, w, h);
        double brightness = (mean.Val0 + mean.Val1 + mean.Val2) / 3.0;
        return brightness is >= 80 and <= 225;
    }

    private static bool LooksLikeBlueRegion(Mat screenshot, double x, double y, double w, double h)
    {
        Scalar mean = MeanColor(screenshot, x, y, w, h);
        double blue = mean.Val0;
        double green = mean.Val1;
        double red = mean.Val2;
        double brightness = (blue + green + red) / 3.0;

        return brightness >= 80 &&
               blue > green + 25 &&
               blue > red + 70;
    }

    private static double MeanBrightness(Mat screenshot, double x, double y, double w, double h)
    {
        Scalar mean = MeanColor(screenshot, x, y, w, h);
        return (mean.Val0 + mean.Val1 + mean.Val2) / 3.0;
    }

    private static Scalar MeanColor(Mat screenshot, double x, double y, double w, double h)
    {
        Rect rect = ToRect(screenshot, x, y, w, h);
        using var region = new Mat(screenshot, rect);
        return Cv2.Mean(region);
    }

    private static Rect ToRect(Mat screenshot, double x, double y, double w, double h)
    {
        int px = Math.Clamp((int)(screenshot.Width * x), 0, screenshot.Width - 1);
        int py = Math.Clamp((int)(screenshot.Height * y), 0, screenshot.Height - 1);
        int pw = Math.Max(1, (int)(screenshot.Width * w));
        int ph = Math.Max(1, (int)(screenshot.Height * h));
        pw = Math.Min(pw, screenshot.Width - px);
        ph = Math.Min(ph, screenshot.Height - py);
        return new Rect(px, py, pw, ph);
    }
}
