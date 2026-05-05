using System.Text.RegularExpressions;
using OpenCvSharp;
using SleepRunner.Automation.Race.Policy;
using SleepRunner.Recognition;
using SleepRunner.Utils;
using SleepRunner.Vision;

namespace SleepRunner.Automation.Race.Handlers;

public class CardSelectHandler : IRaceHandler
{
    private const double SelectDoneClickX = 0.47;
    private const double SelectDoneClickY = 0.83;
    private const int CardSelectionSettleMs = 800;
    private const int SelectDonePollDelayMs = 180;

    private static readonly (double X, double Y, double W, double H)[] TitleRegions =
    [
        (0.02, 0.02, 0.28, 0.10),
        (0.01, 0.07, 0.22, 0.12),
        (0.00, 0.04, 0.26, 0.16),
    ];

    private static readonly (double X, double Y, double W, double H)[] RewardMarkerRegions =
    [
        (0.00, 0.00, 0.22, 0.08),
        (0.00, 0.00, 0.26, 0.12),
        (0.00, 0.00, 0.30, 0.14),
    ];

    private static readonly (double X, double Y, double W, double H)[] CardTextRegions =
    [
        (0.06, 0.26, 0.27, 0.38),
        (0.365, 0.26, 0.27, 0.38),
        (0.67, 0.26, 0.27, 0.38),
    ];

    private static readonly (double X, double Y)[] CardClickPercents =
    [
        (0.20, 0.48),
        (0.50, 0.48),
        (0.80, 0.48),
    ];

    private static readonly (double X, double Y, double W, double H)[] RecommendBadgeRegions =
    [
        (0.16, 0.20, 0.20, 0.10),
        (0.37, 0.20, 0.20, 0.10),
        (0.58, 0.20, 0.20, 0.10),
    ];

    public string Name => "卡片选择";

    public int Priority => 10;

    private readonly Func<Mat, double, double, double, double, string> _readRegion;

    public CardSelectHandler()
        : this(ReadRegionText)
    {
    }

    public CardSelectHandler(Func<Mat, double, double, double, double, string> readRegion)
    {
        _readRegion = readRegion ?? throw new ArgumentNullException(nameof(readRegion));
    }

    public bool CanHandle(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        string title = ReadCardSelectTitle(screenshot);
        if (IsCardSelectTitle(title))
        {
            Log.Log($"CanHandle: OCR title matched card select ('{title}')");
            return true;
        }

        return false;
    }

    public string DescribeDecision(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        string title = ReadCardSelectTitle(screenshot);
        var texts = new string[3];
        for (int i = 0; i < texts.Length; i++)
        {
            var region = CardTextRegions[i];
            string raw = _readRegion(screenshot, region.X, region.Y, region.W, region.H);
            texts[i] = NormalizeOcr(raw);
        }

        var (policy, reason) = ResolvePolicy(title, texts);
        return IsPriorityPolicy(policy)
            ? $"CardSelect: priority rule ({reason})"
            : $"CardSelect: default policy ({reason})";
    }

    public async Task HandleAsync(GameContext ctx)
    {
        Log.Log($"Card select screen detected, profile='{RaceProfileManager.CurrentCardsProfile}'");

        using var shot = ctx.CaptureScreen();
        if (shot == null || shot.Empty())
        {
            Log.Log("ERROR: capture empty, abort card select");
            return;
        }

        CardSelectHandleResult result = await HandleByProfileAsync(ctx, shot);
        if (result == CardSelectHandleResult.HandledWithoutConfirm)
        {
            return;
        }

        if (result == CardSelectHandleResult.NoSelectionReady)
        {
            Log.Log("Card select: select-done stayed gray after trying candidate cards, skip confirm click.");
            return;
        }

        Log.Log("Clicking select done...");
        await ctx.ClickAtPercent(SelectDoneClickX, SelectDoneClickY);
        await ctx.Wait(1000);
    }

    private async Task<CardSelectHandleResult> HandleByProfileAsync(GameContext ctx, Mat shot)
    {
        string title = ReadCardSelectTitle(shot);

        var texts = new string[3];
        for (int i = 0; i < texts.Length; i++)
        {
            var region = CardTextRegions[i];
            string raw = _readRegion(shot, region.X, region.Y, region.W, region.H);
            texts[i] = NormalizeOcr(raw);
            Log.Log($"Card[{i + 1}] OCR: '{texts[i]}'");
            if (CardSelectPlanner.IsQuantityCappedCard(texts[i]))
            {
                Log.Log($"Card[{i + 1}] marked quantity-capped by OCR, will try it after available cards.");
            }
        }

        var (policy, reason) = ResolvePolicy(title, texts);
        Log.Log($"Policy resolved: {policy} ({reason})");

        int? recommendedSlot = TryResolveRecommendedSlot(shot);
        int[] attemptOrder;
        if (IsPriorityPolicy(policy))
        {
            attemptOrder = BuildPriorityAttemptOrder(texts);
            if (CardSelectPlanner.ShouldClickUnselectedForPriorityMiss(policy == CardPolicyType.WhitelistPriorityRule, attemptOrder.Length))
            {
                var unselected = CardSelectPlanner.GetUnselectedClickPercent();
                Log.Log($"Whitelist matched but no priority keyword hit, click unselected at ({unselected.X:F3},{unselected.Y:F3})");
                await ctx.ClickAtPercent(unselected.X, unselected.Y);
                await ctx.Wait(500);
                return CardSelectHandleResult.HandledWithoutConfirm;
            }

            if (attemptOrder.Length == 0)
            {
                Log.Log("Priority rule found no keyword hit, fallback to default policy");
                attemptOrder = BuildDefaultAttemptOrder(recommendedSlot);
            }
        }
        else
        {
            attemptOrder = BuildDefaultAttemptOrder(recommendedSlot);
        }

        attemptOrder = CardSelectPlanner.BuildAttemptOrder(texts, attemptOrder);
        Log.Log($"Card attempt order: {string.Join(" -> ", attemptOrder.Select(slot => $"{slot + 1}{(CardSelectPlanner.IsQuantityCappedCard(texts[slot]) ? "(capped)" : "")}"))}");

        foreach (int slot in attemptOrder)
        {
            var click = CardClickPercents[slot];
            Log.Log($"Card attempt: click index {slot + 1} at ({click.X:F2},{click.Y:F2})");
            await ctx.ClickAtPercent(click.X, click.Y);
            await ctx.Wait(CardSelectionSettleMs);

            if (await WaitForSelectDoneEnabledAsync(ctx, slot))
            {
                return CardSelectHandleResult.SelectionReady;
            }

            Log.Log($"Card attempt: select-done still gray after card {slot + 1}, try next candidate.");
        }

        return CardSelectHandleResult.NoSelectionReady;
    }

    private int[] BuildPriorityAttemptOrder(string[] normalizedTexts)
    {
        var ranked = new List<(int Slot, int Rank)>();
        for (int i = 0; i < normalizedTexts.Length; i++)
        {
            int rank = ClassifyCardRank(normalizedTexts[i]);
            if (rank >= 0)
            {
                ranked.Add((i, rank));
            }
        }

        ranked.Sort((a, b) =>
        {
            int rankCmp = a.Rank.CompareTo(b.Rank);
            return rankCmp != 0 ? rankCmp : a.Slot.CompareTo(b.Slot);
        });

        return [.. ranked.Select(item => item.Slot)];
    }

    private static int[] BuildDefaultAttemptOrder(int? recommendedSlot)
    {
        if (!recommendedSlot.HasValue)
        {
            return [0, 1, 2];
        }

        var order = new List<int> { recommendedSlot.Value };
        for (int i = 0; i < 3; i++)
        {
            if (i != recommendedSlot.Value)
            {
                order.Add(i);
            }
        }

        return [.. order];
    }

    private int ClassifyCardRank(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return -1;
        }

        if (text.Contains("手套", StringComparison.Ordinal))
        {
            int rank = ResolveLabelRank("攻击力", "攻击");
            if (rank >= 0)
            {
                return rank;
            }
        }

        if (text.Contains("项链", StringComparison.Ordinal) || text.Contains("项炼", StringComparison.Ordinal))
        {
            int rank = ResolveLabelRank("暴击伤害", "爆伤");
            if (rank >= 0)
            {
                return rank;
            }
        }

        if (text.Contains("裤子", StringComparison.Ordinal))
        {
            return -1;
        }

        int configuredRank = RaceUserPolicy.ResolvePriorityRank(text);
        if (configuredRank >= 0)
        {
            return configuredRank;
        }

        bool looksLikeAttack =
            (text.Contains("史莱姆", StringComparison.Ordinal) && text.Contains("手套", StringComparison.Ordinal)) ||
            (text.Contains("启示录", StringComparison.Ordinal) && text.Contains("新型", StringComparison.Ordinal) && text.Contains("手套", StringComparison.Ordinal)) ||
            (text.Contains("自身的增加", StringComparison.Ordinal) && text.Contains("%", StringComparison.Ordinal) &&
             !text.Contains("暴击伤害", StringComparison.Ordinal) && !text.Contains("防御力", StringComparison.Ordinal));

        if (looksLikeAttack)
        {
            int rank = ResolveLabelRank("攻击力", "攻击");
            if (rank >= 0)
            {
                return rank;
            }
        }

        return -1;
    }

    private int ResolveLabelRank(params string[] candidateKeywords)
    {
        var order = RaceUserPolicy.CardPriorityOrder;
        for (int i = 0; i < order.Count; i++)
        {
            foreach (string keyword in candidateKeywords)
            {
                if (string.IsNullOrEmpty(keyword))
                {
                    continue;
                }

                if (order[i].Label.Contains(keyword, StringComparison.Ordinal))
                {
                    return i;
                }

                foreach (string configuredKeyword in order[i].Keywords)
                {
                    if (!string.IsNullOrEmpty(configuredKeyword) &&
                        configuredKeyword.Contains(keyword, StringComparison.Ordinal))
                    {
                        return i;
                    }
                }
            }
        }

        return -1;
    }

    private int? TryResolveRecommendedSlot(Mat shot)
    {
        for (int i = 0; i < RecommendBadgeRegions.Length; i++)
        {
            var r = RecommendBadgeRegions[i];
            string raw = _readRegion(shot, r.X, r.Y, r.W, r.H);
            string text = NormalizeOcr(raw);
            Log.Log($"Default policy: recommend OCR slot {i + 1}: '{text}'");
            if (text.Contains("推荐", StringComparison.Ordinal) ||
                text.Contains("推薦", StringComparison.Ordinal))
            {
                Log.Log($"Default policy: recommend OCR found in slot {i + 1} ('{text}')");
                return i;
            }
        }

        int? badgeSlot = CardSelectPlanner.TryResolveRecommendedBadgeSlot(shot, out double[] blueRatios);
        if (badgeSlot.HasValue)
        {
            Log.Log($"Default policy: recommend blue badge found in slot {badgeSlot.Value + 1} (blue ratios: {FormatBlueRatios(blueRatios)})");
            return badgeSlot;
        }

        Log.Log($"Default policy: recommend OCR/color not found (blue ratios: {FormatBlueRatios(blueRatios)}), fallback to card order");
        return null;
    }

    private static string FormatBlueRatios(double[] ratios)
    {
        return string.Join(", ", ratios.Select((ratio, index) => $"{index + 1}={ratio:P1}"));
    }

    private async Task<bool> WaitForSelectDoneEnabledAsync(GameContext ctx, int slot)
    {
        const int pollCount = 3;
        for (int poll = 1; poll <= pollCount; poll++)
        {
            if (poll > 1)
            {
                await ctx.Wait(SelectDonePollDelayMs);
            }

            using var verifyShot = ctx.CaptureScreen();
            if (verifyShot == null || verifyShot.Empty())
            {
                continue;
            }

            bool grayDisabled = CardSelectPlanner.IsSelectDoneGrayDisabled(verifyShot, out double satMean, out double valMean);
            bool enabled = !grayDisabled && satMean >= 45;
            Log.Log($"Card[{slot + 1}] select-done poll={poll}: enabled={enabled}, gray={grayDisabled}, sat={satMean:F1}, val={valMean:F1}");
            if (enabled)
            {
                return true;
            }
        }

        return false;
    }

    private (CardPolicyType Policy, string Reason) ResolvePolicy(string title, string[] cardTexts)
    {
        var whitelist = RaceUserPolicy.CardWhitelist;
        if (whitelist.Count == 0)
        {
            return RaceUserPolicy.CardPriorityOrder.Count > 0
                ? (CardPolicyType.DirectPriorityRule, "no whitelist gating, use priority directly")
                : (CardPolicyType.DefaultRecommendFirst, "whitelist empty + priority empty");
        }

        foreach (var rule in whitelist)
        {
            bool titleHit = false;
            foreach (string keyword in rule.TitleKeywords)
            {
                string normalizedKeyword = NormalizeOcr(keyword);
                if (!string.IsNullOrEmpty(normalizedKeyword) &&
                    title.Contains(normalizedKeyword, StringComparison.Ordinal))
                {
                    titleHit = true;
                    break;
                }
            }

            bool cardHit = false;
            foreach (string keyword in rule.CardKeywords)
            {
                string normalizedKeyword = NormalizeOcr(keyword);
                if (string.IsNullOrEmpty(normalizedKeyword))
                {
                    continue;
                }

                foreach (string text in cardTexts)
                {
                    if (text.Contains(normalizedKeyword, StringComparison.Ordinal))
                    {
                        cardHit = true;
                        break;
                    }
                }

                if (cardHit)
                {
                    break;
                }
            }

            if (titleHit || cardHit)
            {
                string hitType = titleHit ? "title keyword hit" : "card keyword hit";
                return (CardPolicyType.WhitelistPriorityRule, $"rule={rule.Id}, {hitType}");
            }
        }

        return (CardPolicyType.DefaultRecommendFirst, "no whitelist rule matched");
    }

    private static bool IsPriorityPolicy(CardPolicyType policy)
    {
        return policy == CardPolicyType.DirectPriorityRule ||
               policy == CardPolicyType.WhitelistPriorityRule;
    }

    private static string NormalizeOcr(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return "";
        }

        string normalized = Regex.Replace(raw, @"[\s\u3000]+", "");
        return normalized.Trim();
    }

    private string ReadCardSelectTitle(Mat screenshot)
    {
        string firstNonEmpty = "";
        foreach (var region in TitleRegions)
        {
            string title = NormalizeOcr(_readRegion(screenshot, region.X, region.Y, region.W, region.H));
            if (string.IsNullOrEmpty(title))
                continue;

            if (IsCardSelectTitle(title))
                return title;

            if (string.IsNullOrEmpty(firstNonEmpty))
                firstNonEmpty = title;
        }

        foreach (var region in RewardMarkerRegions)
        {
            string marker = NormalizeOcr(_readRegion(screenshot, region.X, region.Y, region.W, region.H));
            if (string.IsNullOrEmpty(marker))
                continue;

            if (IsCardSelectTitle(marker))
                return marker;

            if (string.IsNullOrEmpty(firstNonEmpty))
                firstNonEmpty = marker;
        }

        return firstNonEmpty;
    }

    private static bool IsCardSelectTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
            return false;

        return title.Contains("选择奖励", StringComparison.Ordinal) ||
               title.Contains("奖励选择", StringComparison.Ordinal) ||
               title.Contains("选择", StringComparison.Ordinal) && title.Contains("奖励", StringComparison.Ordinal);
    }

    private static string ReadRegionText(Mat screenshot, double x, double y, double w, double h)
    {
        return OcrHelper.RecognizeRegion(screenshot, x, y, w, h)
            .GetAwaiter()
            .GetResult();
    }

    private enum CardPolicyType
    {
        DefaultRecommendFirst,
        DirectPriorityRule,
        WhitelistPriorityRule,
    }

    private enum CardSelectHandleResult
    {
        SelectionReady,
        NoSelectionReady,
        HandledWithoutConfirm,
    }

    private static readonly LogScope Log = new("Race:CardSelect");
}
