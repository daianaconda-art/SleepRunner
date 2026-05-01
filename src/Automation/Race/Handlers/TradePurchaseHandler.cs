using OpenCvSharp;
using SleepRunner.Automation.Race.Handlers.Trade;
using SleepRunner.Input;
using SleepRunner.Recognition;
using SleepRunner.Utils;
using SleepRunner.Vision;
using System.Text.RegularExpressions;

namespace SleepRunner.Automation.Race.Handlers;

/// <summary>
/// 交易界面处理：识别到交易页后执行购买决策（当前配置为只决策不点击购买）
/// </summary>
public class TradePurchaseHandler : IRaceHandler
{
    public string Name => "交易购买决策";
    public int Priority => 17;

    // 交易界面关键词区域：优先使用右侧交易文案，避免左侧全局属性干扰
    private const double BuyKeywordX = 0.68;
    private const double BuyKeywordY = 0.80;
    private const double BuyKeywordW = 0.28;
    private const double BuyKeywordH = 0.16;
    // 交易列表候选区域（未展开详情时常只有商品名，不含“购买”按钮文本）
    private static readonly (double X, double Y, double W, double H)[] TradeListKeywordRegions =
    [
        (0.66, 0.34, 0.30, 0.34),
        (0.55, 0.45, 0.40, 0.35),
        (0.58, 0.40, 0.36, 0.34),
    ];
    // D-DAY 阶段标题 + 右侧商品列表兜底识别
    private const double StageTitleX = 0.01;
    private const double StageTitleY = 0.07;
    private const double StageTitleW = 0.22;
    private const double StageTitleH = 0.12;
    private const double StageOptionX = 0.55;
    private const double StageOptionY = 0.45;
    private const double StageOptionW = 0.40;
    private const double StageOptionH = 0.35;
    // 交易返回后的二选一菜单区域与委托点击坐标
    private const double StageMenuTextX = 0.66;
    private const double StageMenuTextY = 0.36;
    private const double StageMenuTextW = 0.32;
    private const double StageMenuTextH = 0.44;
    private static readonly (double X, double Y, double W, double H)[] StageMenuRegions =
    [
        (0.66, 0.36, 0.32, 0.44),
        (0.58, 0.32, 0.38, 0.48),
        (0.55, 0.30, 0.42, 0.52),
    ];
    private const double CommissionClickX = 0.88;
    private const double CommissionClickY = 0.50;
    // 与交易/委托分流页相同的右侧菜单文本区：用于兼容“普通交易页”OCR落点
    private const double MenuHintX = 0.66;
    private const double MenuHintY = 0.36;
    private const double MenuHintW = 0.32;
    private const double MenuHintH = 0.44;
    // 大区域兜底：某些分辨率下商品描述会跨行落在更宽区域
    private static readonly (double X, double Y, double W, double H)[] BroadTradeTextRegions =
    [
        (0.56, 0.30, 0.40, 0.50),
        (0.60, 0.30, 0.36, 0.56),
        (0.62, 0.32, 0.34, 0.46),
    ];
    // 休息三选项页识别区域：若命中应让路给 RestDecisionHandler
    private const double RestOptionDetectX = 0.56;
    private const double RestOptionDetectY = 0.40;
    private const double RestOptionDetectW = 0.40;
    private const double RestOptionDetectH = 0.36;
    private static readonly (double X, double Y, double W, double H)[] RestConfirmTextRegions =
    [
        (0.78, 0.82, 0.18, 0.14),
        (0.80, 0.84, 0.16, 0.12),
        (0.76, 0.80, 0.20, 0.16),
    ];
    private static readonly string[] TradeKeywords =
    [
        "交易",
        "商店",
        "购买",
        "药水",
        "秘笈",
        "抽奖券",
        "商品券",
    ];

    // 训练页右侧菜单 OCR 区域：力量/体力/韧性/集中/保护 训练 5 行 + Lv.N + 失败率徽章
    // 该区域只在训练选择页出现，命中即可肯定不是交易页
    private const double TrainingMenuX = 0.78;
    private const double TrainingMenuY = 0.13;
    private const double TrainingMenuW = 0.21;
    private const double TrainingMenuH = 0.65;
    // 「Lv.N」OCR 兼容：游戏里偶尔被识别成 "Lv. 6"、"L v 6"、"LV6" 等
    private static readonly Regex TrainingLvPattern = new(@"[Ll][\s\.]*[Vv][\s\.]*\d", RegexOptions.Compiled);

    private readonly ITradeFlowExecutor _executor = new DefaultTradeFlowExecutor();

    public TradePurchaseHandler()
    {
    }

    public bool CanHandle(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        string restOptionText = Normalize(ReadText(screenshot, RestOptionDetectX, RestOptionDetectY, RestOptionDetectW, RestOptionDetectH));
        string restConfirmText = ReadBestRestConfirmText(screenshot);
        if (IsRestDecisionContext(restOptionText, restConfirmText))
        {
            Log.Log($"Trade purchase handler: yield to rest decision (confirm='{ClipText(restConfirmText)}', options='{ClipText(restOptionText)}')");
            return false;
        }

        // 训练页黑名单：右侧菜单 OCR 命中"训练/失败率/Lv.N" 立即让路
        // 修复历史 bug：训练页右侧 5 行训练菜单数字密集，曾被 LooksLikeTradeListText
        // 的"≥2 数字 + 长度≥8"兜底误命中，导致 trade flow 抢走训练页 47 秒
        string trainingMenuText = Normalize(ReadText(screenshot, TrainingMenuX, TrainingMenuY, TrainingMenuW, TrainingMenuH));
        if (LooksLikeTrainingMenu(trainingMenuText))
        {
            Log.Log($"Trade purchase handler: yield to training menu ('{ClipText(trainingMenuText)}')");
            return false;
        }

        string buyText = Normalize(ReadText(screenshot, BuyKeywordX, BuyKeywordY, BuyKeywordW, BuyKeywordH));
        if (buyText.Contains("购买", StringComparison.Ordinal))
        {
            Log.Log($"Trade purchase handler: buy keyword hit ('{ClipText(buyText)}')");
            return true;
        }

        string effectText = "";
        string stageTitle = Normalize(ReadText(screenshot, StageTitleX, StageTitleY, StageTitleW, StageTitleH));
        string stageOption = Normalize(ReadText(screenshot, StageOptionX, StageOptionY, StageOptionW, StageOptionH));

        if (LooksLikeEventChoiceText(stageOption))
        {
            Log.Log($"Trade purchase handler: skip event-like option text ('{ClipText(stageOption)}')");
            return false;
        }

        // 兜底：交易列表页（无详情展开）也要命中，后续由执行器决定是否购买并可自动返回
        string bestListText = "";
        foreach (var r in TradeListKeywordRegions)
        {
            string text = Normalize(ReadText(screenshot, r.X, r.Y, r.W, r.H));
            if (string.IsNullOrEmpty(text))
                continue;
            if (text.Length > bestListText.Length)
                bestListText = text;

            if (LooksLikeEventChoiceText(text))
                continue;

            bool hasTradeItemHint = LooksLikeTradeListText(text) ||
                                    text.Contains("OPEN", StringComparison.OrdinalIgnoreCase) ||
                                    text.Contains("PEN", StringComparison.OrdinalIgnoreCase);
            if (hasTradeItemHint)
            {
                Log.Log($"Trade purchase handler: list hint hit ('{ClipText(text)}')");
                return true;
            }
        }

        if (!string.IsNullOrEmpty(bestListText))
            Log.Log($"Trade purchase handler: list hint miss ('{ClipText(bestListText)}')");

        // 读取同区域菜单文案仅用于日志，不作为交易页命中条件，避免抢占二选一分流页
        string menuHintText = Normalize(ReadText(screenshot, MenuHintX, MenuHintY, MenuHintW, MenuHintH));

        // 大范围 OCR 兜底：命中交易关键词 + 价格数字即认为在交易页
        string broadBest = "";
        foreach (var r in BroadTradeTextRegions)
        {
            string text = Normalize(ReadText(screenshot, r.X, r.Y, r.W, r.H));
            if (string.IsNullOrEmpty(text))
                continue;
            if (text.Length > broadBest.Length)
                broadBest = text;

            if (LooksLikeEventChoiceText(text))
                continue;

            if (LooksLikeTradeListText(text))
            {
                Log.Log($"Trade purchase handler: broad fallback hit ('{ClipText(text)}')");
                return true;
            }
        }
        if (!string.IsNullOrEmpty(broadBest))
            Log.Log($"Trade purchase handler: broad fallback miss ('{ClipText(broadBest)}')");

        // 注意：不再对“评鉴战/交易”二选一分流页做 stage fallback，
        // 避免在分流页抢先于 TradeAndAppraiseHandler 命中。
        if (!string.IsNullOrEmpty(buyText) || !string.IsNullOrEmpty(effectText) || !string.IsNullOrEmpty(menuHintText) || !string.IsNullOrEmpty(stageTitle) || !string.IsNullOrEmpty(stageOption))
        {
            Log.Log($"Trade purchase handler: final miss (buy='{ClipText(buyText)}', effect='{ClipText(effectText)}', menu='{ClipText(menuHintText)}', title='{ClipText(stageTitle)}', option='{ClipText(stageOption)}')");
        }
        return false;
    }

    public string DescribeDecision(FrameContext frame)
    {
        int detectedBudget = TradeDetailOcr.ReadCurrentMoney(frame.Screenshot);
        if (!TradeBudgetPolicy.TryResolveBudget(detectedBudget, out _))
            return "Trade screen: budget OCR unavailable -> evaluate offers by trade state";

        return "Trade screen: evaluate offers and buy must-buy items";
    }

    public async Task HandleAsync(GameContext ctx)
    {
        Log.Log("Trade purchase handler: start evaluate offers.");
        TradeExecutionResult execResult = await _executor.ExecuteAsync(ctx);
        Log.Log($"Trade purchase handler: executor finished, result={execResult}");

        if (!TradeExecutionResultPolicy.ShouldExitTrade(execResult))
        {
            Log.Log("Trade purchase handler: manual required, stay on trade screen.");
            return;
        }

        // 无论买没买成，交易分支结束后都要尝试收尾回到分流页；
        // 否则会停留在交易页，后续没有 handler 接手。
        await ExitTradeIfStillOnScreenAsync(ctx);
        await TryClickCommissionAfterTradeExitAsync(ctx);
        await ctx.Wait(300);
    }

    /// <summary>
    /// 探测模式：只移动到预计处理的交易项，不执行点击
    /// </summary>
    public async Task ProbeAsync(GameContext ctx)
    {
        using var shot = ctx.CaptureScreen();
        if (shot == null || shot.Empty())
        {
            Log.Log("Trade purchase probe: capture empty, skip move.");
            return;
        }

        var target = ResolveProbePoint(shot);
        int x = (int)(shot.Width * target.X);
        int y = (int)(shot.Height * target.Y);
        Log.Log($"Trade purchase probe: move to ({target.X:F3},{target.Y:F3}), reason='{target.Reason}'");
        await MouseSimulator.MoveToClient(ctx.WindowHandle, x, y);
        await ctx.Wait(300);
    }

    /// <summary>
    /// 若仍停留在交易页则发送 Esc 返回；只发一次避免误弹设置
    /// </summary>
    private async Task ExitTradeIfStillOnScreenAsync(GameContext ctx)
    {
        const int maxEscAttempts = 2;
        for (int attempt = 1; attempt <= maxEscAttempts; attempt++)
        {
            using var shot = ctx.CaptureScreen();
            if (shot == null || shot.Empty())
                return;

            string stageTitle = Normalize(ReadText(shot, StageTitleX, StageTitleY, StageTitleW, StageTitleH));
            string menuText = ReadBestStageMenuText(shot);
            bool stageMenuReady = ContainsCommissionHint(menuText) && !ContainsStrongTradeHint(menuText);
            bool tradeScreen = TradeStageOcr.IsTradeScreen(shot);
            bool shouldSendEsc = TradeExitEscPolicy.ShouldSendEsc(attempt, tradeScreen, stageMenuReady);
            Log.Log($"Trade purchase handler: exit check attempt={attempt}, tradeScreen={tradeScreen}, stageMenuReady={stageMenuReady}, shouldSendEsc={shouldSendEsc}, title='{ClipText(stageTitle)}', menu='{ClipText(menuText)}'");

            if (!shouldSendEsc)
                return;

            Log.Log($"Trade purchase handler: send ESC to return after trade scan (attempt={attempt}, tradeScreen={tradeScreen}).");
            await KeyboardSimulator.SendKey(ctx.WindowHandle, KeyboardSimulator.VK_ESCAPE);
            await ctx.Wait(900);
        }
    }

    /// <summary>
    /// 交易退出后若回到“委托/交易”分流菜单，则直接点击委托
    /// </summary>
    private async Task TryClickCommissionAfterTradeExitAsync(GameContext ctx)
    {
        using var shot = ctx.CaptureScreen();
        if (shot == null || shot.Empty())
            return;

        string menuText = ReadBestStageMenuText(shot);

        bool commissionHint = ContainsCommissionHint(menuText);
        if (!commissionHint)
        {
            string stageTitle = Normalize(ReadText(shot, StageTitleX, StageTitleY, StageTitleW, StageTitleH));
            commissionHint = stageTitle.Contains("评鉴战", StringComparison.Ordinal) || stageTitle.Contains("目标", StringComparison.Ordinal);
            if (!commissionHint)
            {
                Log.Log($"Trade purchase handler: commission menu miss after ESC (menu='{ClipText(menuText)}', title='{ClipText(stageTitle)}').");
                return;
            }
        }

        if (ContainsStrongTradeHint(menuText))
        {
            Log.Log($"Trade purchase handler: commission click skipped (still trade-like menu='{ClipText(menuText)}').");
            return;
        }

        Log.Log($"Trade purchase handler: returned to stage menu, click commission ('{ClipText(menuText)}').");
        await ctx.ClickAtPercent(CommissionClickX, CommissionClickY);
        await ctx.Wait(1200);
    }

    private static string ReadText(Mat screenshot, double x, double y, double w, double h)
    {
        return OcrHelper.RecognizeRegion(screenshot, x, y, w, h)
            .GetAwaiter()
            .GetResult();
    }

    private static string Normalize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        return raw
            .Replace(" ", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace("\u3000", "")
            .Trim();
    }

    /// <summary>
    /// 判断文本是否包含交易关键词
    /// </summary>
    private static bool ContainsTradeKeyword(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        foreach (var kw in TradeKeywords)
        {
            if (text.Contains(kw, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 判断文本是否包含强交易商品关键词，避免被属性面板误判
    /// </summary>
    private static bool ContainsTradeItemKeyword(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        return text.Contains("药水", StringComparison.Ordinal) ||
               text.Contains("秘笈", StringComparison.Ordinal) ||
               text.Contains("抽奖券", StringComparison.Ordinal) ||
               text.Contains("商品券", StringComparison.Ordinal) ||
               text.Contains("鸡排", StringComparison.Ordinal) ||
               text.Contains("蛋糕", StringComparison.Ordinal) ||
               text.Contains("义大利面", StringComparison.Ordinal) ||
               text.Contains("意大利面", StringComparison.Ordinal) ||
               text.Contains("牛肉", StringComparison.Ordinal) ||
               text.Contains("料理食物", StringComparison.Ordinal) ||
               text.Contains("沙拉", StringComparison.Ordinal) ||
               text.Contains("甜甜圈", StringComparison.Ordinal) ||
               text.Contains("炖菜", StringComparison.Ordinal);
    }

    private static bool LooksLikeTradeListText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        if (ContainsTradeItemKeyword(text))
            return true;

        if (text.Contains("SOLD", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("OUT", StringComparison.OrdinalIgnoreCase))
            return true;

        // 纯数字兜底必须同时含交易语义关键字，避免训练页 Lv/失败率数字误命中
        // 历史 bug：仅靠 "≥2 数字 + 长度≥8" 会把训练菜单 OCR 当作交易项
        return CountPriceLikeNumbers(text) >= 2
               && text.Length >= 8
               && (ContainsTradeKeyword(text) || ContainsTradeItemKeyword(text));
    }

    /// <summary>
    /// 判断文本是否包含价格样式数字
    /// </summary>
    private static bool ContainsPriceLikeNumber(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        return CountPriceLikeNumbers(text) > 0;
    }

    private static int CountPriceLikeNumbers(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        return Regex.Matches(text, @"\d{1,3}").Count;
    }

    /// <summary>
    /// 判断右侧菜单文本是否是训练选择页：5 行 "XX训练 Lv.N" + 失败率徽章 极独特
    /// 任一命中即可肯定不是交易页（药水菜单不会写"训练/失败率/Lv."）
    /// </summary>
    private static bool LooksLikeTrainingMenu(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        // "失败率"三字几乎只在训练选择页出现
        if (text.Contains("失败率", StringComparison.Ordinal))
            return true;

        // "训练" 5 行至少能命中 2 个，OCR 极坏时也能保住一个
        int trainCount = CountOccurrences(text, "训练");
        if (trainCount >= 2)
            return true;

        // "Lv.N" 5 行至少 2 个能稳定命中
        int lvCount = TrainingLvPattern.Matches(text).Count;
        if (lvCount >= 2)
            return true;

        // 单一 "训练" + 单一 "Lv.N" 双线索也算
        if (trainCount >= 1 && lvCount >= 1)
            return true;

        return false;
    }

    private static int CountOccurrences(string source, string token)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(token))
            return 0;
        int count = 0;
        int idx = 0;
        while ((idx = source.IndexOf(token, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += token.Length;
        }
        return count;
    }

    private static bool LooksLikeEventChoiceText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        int sentenceCount = text.Count(c => c == '。');
        int plusCount = text.Count(c => c == '+');
        bool hasChoiceSentence = sentenceCount >= 2 || plusCount >= 1;
        bool hasBoxChoice = (text.Contains("左边", StringComparison.Ordinal) || text.Contains("右边", StringComparison.Ordinal) ||
                             text.Contains("左侧", StringComparison.Ordinal) || text.Contains("右侧", StringComparison.Ordinal)) &&
                            (text.Contains("箱子", StringComparison.Ordinal) || text.Contains("盒子", StringComparison.Ordinal));
        bool hasWaitChoice = text.Contains("等待下次机会", StringComparison.Ordinal) ||
                             text.Contains("下次机会", StringComparison.Ordinal);
        bool hasActionVerb = text.Contains("将", StringComparison.Ordinal) ||
                             text.Contains("投入", StringComparison.Ordinal) ||
                             text.Contains("放入", StringComparison.Ordinal) ||
                             text.Contains("等待", StringComparison.Ordinal);

        // 强事件指纹：≥2 个 +号选项 一定是事件二选一对话
        // （例：'+不，今天训练到此为止... +好，再试一次吧！'）
        if (plusCount >= 2)
            return true;

        // 设问句指纹：事件常见叙述格式
        if (text.Contains("该说什么", StringComparison.Ordinal) ||
            text.Contains("该怎么办", StringComparison.Ordinal) ||
            text.Contains("怎么办呢", StringComparison.Ordinal) ||
            text.Contains("什么好呢", StringComparison.Ordinal) ||
            text.Contains("怎么办才好", StringComparison.Ordinal))
            return true;

        // 单 + 号但带常见事件短语也算事件
        if (plusCount >= 1 && (
                text.Contains("再试一次", StringComparison.Ordinal) ||
                text.Contains("到此为止", StringComparison.Ordinal) ||
                text.Contains("再说吧", StringComparison.Ordinal) ||
                text.Contains("算了吧", StringComparison.Ordinal) ||
                text.Contains("放弃吧", StringComparison.Ordinal) ||
                text.Contains("继续吧", StringComparison.Ordinal)))
            return true;

        return (hasChoiceSentence && hasActionVerb) || hasBoxChoice || hasWaitChoice;
    }

    /// <summary>
    /// 判断文本是否包含委托相关线索
    /// </summary>
    private static bool ContainsCommissionHint(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        return text.Contains("委托", StringComparison.Ordinal) ||
               text.Contains("讨伐", StringComparison.Ordinal) ||
               text.Contains("受理", StringComparison.Ordinal);
    }

    /// <summary>
    /// 判断文本是否仍是强交易页线索
    /// </summary>
    private static bool ContainsStrongTradeHint(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        return text.Contains("购买", StringComparison.Ordinal) ||
               ContainsTradeItemKeyword(text) ||
               text.Contains("药水", StringComparison.Ordinal) ||
               text.Contains("秘笈", StringComparison.Ordinal) ||
               text.Contains("抽奖券", StringComparison.Ordinal) ||
               text.Contains("商品券", StringComparison.Ordinal);
    }

    /// <summary>
    /// 严格交易页判定：用于二次 Esc 防误触
    /// </summary>
    private static bool IsStrongTradeScreen(Mat screenshot)
    {
        string restOptionText = Normalize(ReadText(screenshot, RestOptionDetectX, RestOptionDetectY, RestOptionDetectW, RestOptionDetectH));
        string restConfirmText = ReadBestRestConfirmText(screenshot);
        if (IsRestDecisionContext(restOptionText, restConfirmText))
            return false;

        string buyText = Normalize(ReadText(screenshot, BuyKeywordX, BuyKeywordY, BuyKeywordW, BuyKeywordH));
        if (buyText.Contains("购买", StringComparison.Ordinal))
            return true;

        foreach (var r in TradeListKeywordRegions)
        {
            string text = Normalize(ReadText(screenshot, r.X, r.Y, r.W, r.H));
            if (string.IsNullOrEmpty(text))
                continue;
            bool hasTradeItemHint = LooksLikeTradeListText(text) ||
                                    text.Contains("OPEN", StringComparison.OrdinalIgnoreCase) ||
                                    text.Contains("PEN", StringComparison.OrdinalIgnoreCase);
            if (hasTradeItemHint)
                return true;
        }

        string stageTitle = Normalize(ReadText(screenshot, StageTitleX, StageTitleY, StageTitleW, StageTitleH));
        string stageOption = Normalize(ReadText(screenshot, StageOptionX, StageOptionY, StageOptionW, StageOptionH));
        if ((stageTitle.Contains("评鉴战", StringComparison.Ordinal) || stageTitle.Contains("目标", StringComparison.Ordinal)) &&
            ContainsTradeKeyword(stageOption))
            return true;

        return false;
    }

    private static string ReadBestRestConfirmText(Mat screenshot)
    {
        string best = "";
        foreach (var region in RestConfirmTextRegions)
        {
            string text = Normalize(ReadText(screenshot, region.X, region.Y, region.W, region.H));
            if (string.IsNullOrEmpty(text))
                continue;
            if (text.Contains("休息", StringComparison.Ordinal))
                return text;
            if (text.Length > best.Length)
                best = text;
        }

        return best;
    }

    private static bool IsRestDecisionContext(string optionText, string confirmText)
    {
        if (string.IsNullOrEmpty(optionText) || string.IsNullOrEmpty(confirmText))
            return false;

        if (!confirmText.Contains("休息", StringComparison.Ordinal))
            return false;

        int score = 0;
        if (optionText.Contains("免费住处", StringComparison.Ordinal)) score += 4;
        if (optionText.Contains("冥想室", StringComparison.Ordinal)) score += 4;
        if (optionText.Contains("免费", StringComparison.Ordinal)) score += 1;
        if (optionText.Contains("住处", StringComparison.Ordinal)) score += 1;
        if (optionText.Contains("30", StringComparison.Ordinal)) score += 1;
        if (optionText.Contains("60", StringComparison.Ordinal)) score += 1;

        return score >= 5;
    }

    /// <summary>
    /// 截断日志文本，避免OCR长串刷屏
    /// </summary>
    private static string ClipText(string text, int maxLen = 80)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        if (text.Length <= maxLen)
            return text;
        return text[..maxLen] + "...";
    }

    /// <summary>
    /// 根据右侧三行商品文案推断探测移动目标
    /// </summary>
    private static (double X, double Y, string Reason) ResolveProbePoint(Mat screenshot)
    {
        var rows = new (double X, double Y, double W, double H, double ClickY)[]
        {
            (0.62, 0.34, 0.34, 0.12, 0.41),
            (0.62, 0.47, 0.34, 0.12, 0.52),
            (0.62, 0.60, 0.34, 0.12, 0.63),
        };

        int bestScore = int.MinValue;
        double bestY = 0.63;
        string bestText = "";
        foreach (var row in rows)
        {
            string text = Normalize(ReadText(screenshot, row.X, row.Y, row.W, row.H));
            int score = 0;
            if (string.IsNullOrEmpty(text))
                score -= 2;
            if (text.Contains("耐力", StringComparison.Ordinal) ||
                text.Contains("体力", StringComparison.Ordinal) ||
                text.Contains("恢复", StringComparison.Ordinal))
                score += 8;
            if (text.Contains("炖菜", StringComparison.Ordinal) ||
                text.Contains("甜甜圈", StringComparison.Ordinal) ||
                text.Contains("沙拉", StringComparison.Ordinal))
                score += 5;
            if (Regex.IsMatch(text, @"\d{1,3}"))
                score += 2;

            if (score > bestScore)
            {
                bestScore = score;
                bestY = row.ClickY;
                bestText = text;
            }
        }

        if (bestScore < 1)
            return (0.88, 0.52, "fallback-middle-slot");
        return (0.88, bestY, ClipText(bestText, 30));
    }

    private string ReadBestStageMenuText(Mat screenshot)
    {
        string menuText = "";
        foreach (var region in StageMenuRegions)
        {
            string text = Normalize(ReadText(screenshot, region.X, region.Y, region.W, region.H));
            if (text.Length > menuText.Length)
                menuText = text;
            if (ContainsCommissionHint(text))
                return text;
        }

        return menuText;
    }
    private static readonly LogScope Log = new("Race:TradeBuy");
}
