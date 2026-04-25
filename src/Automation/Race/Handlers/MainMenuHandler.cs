using OpenCvSharp;
using SleepRunner.Input;
using SleepRunner.Recognition;
using SleepRunner.Utils;
using SleepRunner.Vision;

namespace SleepRunner.Automation.Race.Handlers;

/// <summary>
/// 跑马主菜单：检测到训练/委托/休息界面时，根据耐力决策
/// 快捷键：Alt+1 训练，Alt+2 委托，Alt+3 休息
/// 当前策略：满耐力点训练
/// </summary>
public class MainMenuHandler : IRaceHandler
{
    public string Name => "主菜单决策";
    public int Priority => 20;

    // 委托提示候选 OCR 区域（不同帧/分辨率下红框会轻微漂移）
    private static readonly (double X, double Y, double W, double H)[] CommissionTipRegions =
    [
        (0.76, 0.50, 0.22, 0.20),
        (0.68, 0.40, 0.30, 0.40),
        (0.72, 0.44, 0.26, 0.30),
        (0.60, 0.42, 0.36, 0.34),
    ];
    private static readonly (double X, double Y, double W, double H)[] StageTitleRegions =
    [
        (0.01, 0.07, 0.22, 0.12),
        (0.00, 0.04, 0.26, 0.16),
    ];
    private static readonly (double X, double Y, double W, double H)[] TradeStageRegions =
    [
        (0.66, 0.36, 0.32, 0.44),
        (0.58, 0.32, 0.38, 0.48),
        (0.55, 0.30, 0.42, 0.52),
    ];

    private bool _lastPreviewHasCommission;
    private string _lastPreviewTipText = "";
    private DateTime _lastPreviewUtc = DateTime.MinValue;

    public bool CanHandle(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        if (LooksLikeTradeAppraiseStage(screenshot, out string stageSummary))
        {
            Log.Log($"Main menu ignored: trade/commission stage detected ({stageSummary})");
            return false;
        }

        bool hit = MainMenuScreenChecks.IsMainMenuScreen(screenshot, out string menuSummary);
        if (hit)
            Log.Log($"Main menu OCR hit: {menuSummary}");
        return hit;
    }

    public string DescribeDecision(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        bool hasCommission = HasRaidCommissionTip(screenshot, out var tipText);
        _lastPreviewHasCommission = hasCommission;
        _lastPreviewTipText = tipText;
        _lastPreviewUtc = DateTime.UtcNow;
        return hasCommission
            ? $"MainMenu: commission tip detected ('{tipText}') -> click commission diamond first"
            : $"MainMenu: no commission tip (best='{tipText}') -> click training diamond";
    }

    public async Task HandleAsync(GameContext ctx)
    {
        // 优先复用预览结论，避免 Step gate 前后帧抖动导致预览与执行分叉
        bool reusePreview = _lastPreviewHasCommission &&
                            (DateTime.UtcNow - _lastPreviewUtc).TotalSeconds <= 10;

        if (reusePreview)
        {
            Log.Log($"Main menu: reuse preview decision ('{_lastPreviewTipText}'), clicking commission diamond.");
            var p = ResolveMenuClickPoint(ctx, preferCommission: true);
            await ctx.ClickAtPercent(p.X, p.Y);
            await ctx.Wait(1500);
            return;
        }

        var stableDetect = await DetectCommissionTipStableAsync(ctx);
        if (stableDetect.Hit)
        {
            Log.Log($"Main menu detected, commission tip hit ('{stableDetect.Text}'), clicking commission diamond...");
            var p = ResolveMenuClickPoint(ctx, preferCommission: true);
            await ctx.ClickAtPercent(p.X, p.Y);
            await ctx.Wait(1500);
            return;
        }

        // 默认路径：进入训练界面
        Log.Log("Main menu detected, no commission tip, clicking train diamond...");
        var trainPoint = ResolveMenuClickPoint(ctx, preferCommission: false);
        await ctx.ClickAtPercent(trainPoint.X, trainPoint.Y);
        await MainMenuTransitionWaiter.WaitForTrainingScreenAsync(ctx);
    }

    /// <summary>
    /// 按右侧三行文本动态定位菜单点击点，优先委托第二行
    /// </summary>
    private static (double X, double Y) ResolveMenuClickPoint(GameContext ctx, bool preferCommission)
    {
        using var shot = ctx.CaptureScreen();
        var point = MainMenuScreenChecks.ResolveMenuClickPoint(shot, preferCommission);
        Log.Log($"Main menu click resolve: prefer={(preferCommission ? "commission" : "train")}, point=({point.X:F3},{point.Y:F3})");
        return point;
    }

    /// <summary>
    /// 多帧检测委托提示，降低单帧 OCR 抖动造成的漏判
    /// </summary>
    private static async Task<(bool Hit, string Text)> DetectCommissionTipStableAsync(GameContext ctx)
    {
        string bestText = "";
        bool hit = false;
        for (int i = 0; i < 3; i++)
        {
            using var shot = ctx.CaptureScreen();
            if (shot != null && !shot.Empty())
            {
                bool oneHit = HasRaidCommissionTip(shot, out var text);
                if (!string.IsNullOrEmpty(text) && text.Length > bestText.Length)
                    bestText = text;
                if (oneHit)
                {
                    hit = true;
                    bestText = text;
                    break;
                }
            }

            if (i < 2)
                await ctx.Wait(120);
        }

        return (hit, bestText);
    }

    /// <summary>
    /// 检测委托红框提示（OCR 关键词：受理讨伐委托）
    /// </summary>
    private static bool HasRaidCommissionTip(Mat screenshot, out string normalizedText)
    {
        normalizedText = "";
        string best = "";

        foreach (var r in CommissionTipRegions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, r.X, r.Y, r.W, r.H)
                .GetAwaiter()
                .GetResult();
            string text = NormalizeOcr(raw);
            if (string.IsNullOrEmpty(text))
                continue;

            if (text.Length > best.Length)
                best = text;

            if (IsCommissionTipText(text))
            {
                normalizedText = text;
                return true;
            }
        }

        normalizedText = best;
        return false;
    }

    /// <summary>
    /// 判断文本是否命中“受理讨伐委托”语义（允许 OCR 断裂）
    /// </summary>
    private static bool IsCommissionTipText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (text.Contains("受理讨伐委托", StringComparison.Ordinal)) return true;
        if (text.Contains("讨伐委托", StringComparison.Ordinal)) return true;

        bool hasCommission = text.Contains("委托", StringComparison.Ordinal);
        bool hasRaid = text.Contains("讨伐", StringComparison.Ordinal);
        bool hasAccept = text.Contains("受理", StringComparison.Ordinal);
        return hasCommission && (hasRaid || hasAccept);
    }

    private static bool LooksLikeTradeAppraiseStage(Mat screenshot, out string summary)
    {
        string title = ReadBestText(screenshot, StageTitleRegions, ContainsAppraiseKeyword);
        if (!ContainsAppraiseKeyword(title))
        {
            summary = $"title='{title}', menu=''";
            return false;
        }

        string menu = ReadBestText(screenshot, TradeStageRegions, ContainsTradeStageKeyword);
        bool hit = ContainsTradeStageKeyword(menu);
        summary = $"title='{title}', menu='{menu}', hit={hit}";
        return hit;
    }

    private static string ReadBestText(
        Mat screenshot,
        (double X, double Y, double W, double H)[] regions,
        Func<string, bool> predicate)
    {
        string best = "";
        foreach (var r in regions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, r.X, r.Y, r.W, r.H)
                .GetAwaiter()
                .GetResult();
            string text = NormalizeOcr(raw);
            if (text.Length > best.Length)
                best = text;
            if (predicate(text))
                return text;
        }

        return best;
    }

    private static bool ContainsAppraiseKeyword(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("评鉴战", StringComparison.Ordinal) ||
               text.Contains("评鉴", StringComparison.Ordinal) ||
               text.Contains("目标评鉴战", StringComparison.Ordinal) ||
               text.Contains("D-DAY", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsTradeStageKeyword(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("交易", StringComparison.Ordinal) ||
               text.Contains("全新商品到货", StringComparison.Ordinal) ||
               text.Contains("商品到货", StringComparison.Ordinal) ||
               text.Contains("到货", StringComparison.Ordinal);
    }

    /// <summary>
    /// OCR 文本归一化：去空格、换行和全角空格
    /// </summary>
    private static string NormalizeOcr(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        return raw
            .Replace(" ", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace("\u3000", "")
            .Trim();
    }

    private static readonly LogScope Log = new("Race:MainMenu");
}
