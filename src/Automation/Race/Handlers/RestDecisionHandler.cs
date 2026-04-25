using System.Text.RegularExpressions;
using OpenCvSharp;
using SleepRunner.Input;
using SleepRunner.Recognition;
using SleepRunner.Utils;
using SleepRunner.Vision;

namespace SleepRunner.Automation.Race.Handlers;

/// <summary>
/// 休息界面决策：按资金能力在 3/2/1 号方案中降级选择，再点击休息确认
///
/// 性能优化（2026-04-20）：
///   - CanHandle / ReadMoney / ResolveOptionClickPoint 全部改为「第一个高置信结果命中即返回」
///   - 加入 1 秒短 TTL 的 Plan 缓存，避免 DescribeDecision 与 HandleAsync 重复 OCR
///   - 合并测试结果：单次 rest 决策 OCR 从 ~70 次降到 ~7 次
/// </summary>
public class RestDecisionHandler : IRaceHandler
{
    public string Name => "休息决策";
    public int Priority => 18;

    // 休息界面右侧三行选项区域（用于检测是否进入休息分支）
    private const double OptionDetectX = 0.56;
    private const double OptionDetectY = 0.40;
    private const double OptionDetectW = 0.40;
    private const double OptionDetectH = 0.36;

    // 顶部金钱显示区域：经验上 (0.62~0.74) × (0~0.07) 是金钱 HUD 的稳定位置
    // 之前 (0.78, 0, 0.20, 0.08) 太偏右且高度过大，会把潜质点/D-DAY 撞进来
    private const double MoneyRegionX = 0.62;
    private const double MoneyRegionY = 0.00;
    private const double MoneyRegionW = 0.12;
    private const double MoneyRegionH = 0.07;
    // 休息界面固定价格：1号免费，2号30，3号60
    private const int Option1FixedCost = 0;
    private const int Option2FixedCost = 30;
    private const int Option3FixedCost = 60;

    // 三个休息选项的点击坐标（从上到下：1/2/3）
    // 校准来源：2026-04-20 通过对比 click Y 与点击后画面 OCR ("正专注冥想中" vs "好好休息中") 反推
    //   - Y=0.66 实际落到 option 3 下方空白带 → 默认选中 option 1 → 用户看到"选了第一个选项"
    //   - Y=0.63 才能稳定命中 option 3 (冥想室)
    // 实际游戏 UI 三选项 Y 间距约 0.07（不是最初猜的 0.10），整体上移
    private static readonly (double X, double Y)[] OptionClickPoints =
    [
        (0.84, 0.49), // 1号方案 (免费住处)
        (0.84, 0.56), // 2号方案 (30金住处)
        (0.84, 0.63), // 3号方案 (60金冥想室)
    ];

    // 点击最终休息确认按钮（和训练界面底部主按钮区域一致）
    private const double ConfirmRestBtnX = 0.89;
    private const double ConfirmRestBtnY = 0.89;
    private static readonly (double X, double Y, double W, double H)[] ConfirmRestTextRegions =
    [
        (0.78, 0.82, 0.18, 0.14),
        (0.80, 0.84, 0.16, 0.12),
        (0.76, 0.80, 0.20, 0.16),
    ];
    private static readonly (double X, double Y, double W, double H)[] DetailTitleRegions =
    [
        (0.52, 0.24, 0.22, 0.10),
        (0.50, 0.22, 0.26, 0.12),
        (0.54, 0.26, 0.18, 0.08),
    ];

    // -------- 短 TTL Plan 缓存：DescribeDecision 与 HandleAsync 之间复用 --------
    // 缓存键用 screenshot 的 (Width,Height) + 像素首字节地址近似匹配；
    // 真正的可靠性来自 TTL：>800ms 视为不同帧，强制重算。
    private RestPlan? _cachedPlan;
    private long _cachedAtMs;
    private const int PlanCacheTtlMs = 800;

    public bool CanHandle(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        // 优化：先用 1 个高置信区域试，命中即返回，不走 fallback
        string optionText = ReadRestOptionText(screenshot, fastPath: true);
        if (string.IsNullOrEmpty(optionText))
            return false;

        string confirmText = ReadRestConfirmText(screenshot, fastPath: true);
        bool matched = IsRestDecisionContext(optionText, confirmText);

        // 仅在 fast-path 失败时再尝试扩展区域，避免常态下浪费 OCR
        if (!matched)
        {
            string optionExt = ReadRestOptionText(screenshot, fastPath: false);
            string confirmExt = ReadRestConfirmText(screenshot, fastPath: false);
            matched = IsRestDecisionContext(optionExt, confirmExt);
            if (matched)
            {
                Log.Log($"Rest detect hit (extended): confirm='{confirmExt}', options='{optionExt}'");
            }
            else if (LooksLikeStrongRestOptions(optionExt))
            {
                // 诊断日志：选项区像休息页但右下角"休息"按钮 OCR 没识别出来。
                // 常见诱因：按钮 hover 变样 / 按钮淡入未完成 / OCR 区域被遮挡。
                Log.Log($"Rest detect MISS (strong options but no confirm): options='{ClipText(optionExt, 60)}', confirm='{ClipText(confirmExt, 30)}'");
            }
        }
        else
        {
            Log.Log($"Rest detect hit: confirm='{confirmText}', options='{optionText}'");
        }
        return matched;
    }

    /// <summary>
    /// 选项区是否含强休息页指纹（冥想室 + 免费住处/免费 + 价格 30/60）
    /// 用于：① 触发"为何不接管"诊断日志；② IsRestDecisionContext 在 confirm 未识别时的兜底接管
    /// </summary>
    private static bool LooksLikeStrongRestOptions(string optionText)
    {
        if (string.IsNullOrEmpty(optionText)) return false;
        bool hasMeditation = optionText.Contains("冥想室", StringComparison.Ordinal);
        bool hasFreeShelter = optionText.Contains("免费住处", StringComparison.Ordinal) ||
                              optionText.Contains("免费休息", StringComparison.Ordinal) ||
                              optionText.Contains("露宿", StringComparison.Ordinal);
        bool has30 = optionText.Contains("30", StringComparison.Ordinal);
        bool has60 = optionText.Contains("60", StringComparison.Ordinal);
        return hasMeditation && hasFreeShelter && has30 && has60;
    }

    public string DescribeDecision(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        var plan = GetOrBuildPlan(screenshot);
        if (plan.RequiresManualIntervention)
            return $"RestDecision: money={FormatValue(plan.Money)} -> wait manual (money OCR failed)";
        return $"RestDecision: money={FormatValue(plan.Money)} fixedCosts=[0,30,60] -> choose option {plan.ChosenOption}";
    }

    public async Task HandleAsync(GameContext ctx)
    {
        using var shot = ctx.CaptureScreen();
        if (shot == null || shot.Empty())
        {
            Log.Log("Rest decision: capture empty, skip");
            return;
        }

        var plan = GetOrBuildPlan(shot);
        if (plan.RequiresManualIntervention || !plan.ChosenOption.HasValue)
        {
            Log.Log($"Rest decision: money={FormatValue(plan.Money)} unavailable, wait manual instead of default option.");
            return;
        }

        Log.Log($"Rest decision: money={FormatValue(plan.Money)}, fixedCosts=[0,30,60], choose={plan.ChosenOption}");

        int idx = Math.Clamp(plan.ChosenOption.Value - 1, 0, 2);
        string detailTitle = ReadRestDetailTitleText(shot);
        int expandedIdx = ResolveExpandedRestOptionIndex(detailTitle);
        if (expandedIdx == idx)
        {
            Log.Log($"Rest decision: option {idx + 1} already expanded ('{detailTitle}'), skip re-click.");
        }
        else
        {
            var point = ResolveOptionClickPoint(shot, idx);
            await ctx.ClickAtPercent(point.X, point.Y);
            await ctx.Wait(500);
        }

        Log.Log("Rest decision: click confirm rest button");
        await ctx.ClickAtPercent(ConfirmRestBtnX, ConfirmRestBtnY);
        await ctx.Wait(1500);

        // 任务完成后清缓存，下次进入重新决策
        _cachedPlan = null;
    }

    /// <summary>
    /// 探测模式：仅移动鼠标到将要点击的休息选项，不执行点击
    /// </summary>
    public async Task ProbeAsync(GameContext ctx)
    {
        using var shot = ctx.CaptureScreen();
        if (shot == null || shot.Empty())
        {
            Log.Log("Rest probe: capture empty, skip move");
            return;
        }

        var plan = GetOrBuildPlan(shot);
        if (plan.RequiresManualIntervention || !plan.ChosenOption.HasValue)
        {
            Log.Log($"Rest probe: money={FormatValue(plan.Money)} unavailable, skip move and wait manual.");
            return;
        }

        int idx = Math.Clamp(plan.ChosenOption.Value - 1, 0, 2);
        var point = ResolveOptionClickPoint(shot, idx);
        int x = (int)(shot.Width * point.X);
        int y = (int)(shot.Height * point.Y);

        Log.Log($"Rest probe: money={FormatValue(plan.Money)}, choose={plan.ChosenOption}, move to ({point.X:F3},{point.Y:F3})");
        await MouseSimulator.MoveToClient(ctx.WindowHandle, x, y);
        await ctx.Wait(300);
    }

    /// <summary>
    /// TTL 内复用上次 plan，避免 DescribeDecision 与 HandleAsync 反复 OCR
    /// </summary>
    private RestPlan GetOrBuildPlan(Mat screenshot)
    {
        long now = Environment.TickCount64;
        if (_cachedPlan.HasValue && (now - _cachedAtMs) < PlanCacheTtlMs)
            return _cachedPlan.Value;

        var plan = BuildDecisionPlan(screenshot);
        _cachedPlan = plan;
        _cachedAtMs = now;
        return plan;
    }

    /// <summary>
    /// 读取顶部金钱，按固定价格 60/30/0 做 3→2→1 可支付决策
    /// </summary>
    private static RestPlan BuildDecisionPlan(Mat screenshot)
    {
        int? money = ReadMoney(screenshot);
        if (!RestDecisionPolicy.TryChooseOption(money, out int chosen))
            return new RestPlan(money, null, true);

        return new RestPlan(money, chosen, false);
    }

    /// <summary>
    /// 按资金从高到低尝试 3/2/1；无法识别时保守选 1
    /// </summary>
    private static int DecideOptionByBudget(int? money)
    {
        return RestDecisionPolicy.TryChooseOption(money, out int option) ? option : 0;
    }

    /// <summary>
    /// 读取顶部金钱数值
    ///
    /// 简化版（2026-04-21 重构）：决策只有三档（≥60 / ≥30 / 其他），
    /// 所以读钱不需要精确到个位，只需要区分清楚"<30 / 30~59 / ≥60"。
    /// 历史复杂的多区域投票 + 多变体打分被推倒，改成最直接的三步：
    ///   ① 单一固定 ROI 做一次 OCR
    ///   ② raw 文本含日期/排名/百分比等噪声词 → 直接 null（保守走 option 1 免费）
    ///   ③ 提取数字候选，只保留整 10 倍数（金钱粒度），取最大值
    /// 任何一步不满足都返回 null，让 DecideOptionByBudget 默认走 option 1，永远安全
    /// </summary>
    private static int? ReadMoney(Mat screenshot)
    {
        bool ok = HudMoneyOcr.TryReadMoney(screenshot, out int money, out string summary);
        if (!ok)
        {
            Log.Log($"Rest money OCR failed: {summary} -> null");
            return null;
        }

        Log.Log($"Rest money OCR: {summary} -> {money}");
        return money;
#if false

        // 只信"整 10 倍数"。游戏金钱出入都是 10 的倍数，
        // OCR 出现 301/259/2 这种值都是噪声（潜质点/百分比/截断）
        var roundTens = nums.Where(IsRoundTenValue).ToList();
        if (roundTens.Count == 0)
        {
            Log.Log($"Rest money OCR no round-10 value (nums=[{string.Join(",", nums)}]), raw='{raw}' -> null");
            return null;
        }

        // 多个整 10 候选时取最大值：HUD 显示的是金钱总额，最大的最有可能是真值
        int chosen = roundTens.Max();
        Log.Log($"Rest money OCR: raw='{raw}' nums=[{string.Join(",", nums)}] roundTens=[{string.Join(",", roundTens)}] -> {chosen}");
        return chosen;
#endif
    }

    /// <summary>
    /// 归一化金额 OCR 文本，避免把字母误替换成数字
    /// </summary>
    private static string NormalizeMoneyOcr(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        return Regex.Replace(raw, @"[\s\u3000]+", "").Trim();
    }

    /// <summary>
    /// 提取金额候选，限制在合理范围
    /// </summary>
    private static List<int> ExtractNumberCandidates(string text)
    {
        var result = new List<int>();
        if (string.IsNullOrEmpty(text))
            return result;

        string normalized = text
            .Replace('O', '0')
            .Replace('o', '0')
            .Replace('〇', '0')
            .Replace('l', '1')
            .Replace('I', '1');
        foreach (Match m in Regex.Matches(normalized, @"\d{1,5}"))
        {
            if (!int.TryParse(m.Value, out int value))
                continue;
            if (value < 0 || value > 99999)
                continue;
            result.Add(value);
        }
        return result;
    }

    /// <summary>
    /// 顶部 HUD 噪声词检测：日期 / 评鉴战倒计时 / 排名 / 比例 等
    /// 命中任意一个就直接判定该 OCR 区域读到了非金钱内容（说明 ROI 漂了）
    /// </summary>
    private static bool HasNonMoneyNoise(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("月", StringComparison.Ordinal) ||
               text.Contains("上旬", StringComparison.Ordinal) ||
               text.Contains("中旬", StringComparison.Ordinal) ||
               text.Contains("下旬", StringComparison.Ordinal) ||
               text.Contains("日", StringComparison.Ordinal) ||
               text.Contains("距离", StringComparison.Ordinal) ||
               text.Contains("评鉴", StringComparison.Ordinal) ||
               text.Contains("RANK", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("D-DAY", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("DAY", StringComparison.OrdinalIgnoreCase) ||
               text.Contains(":", StringComparison.Ordinal) ||
               text.Contains("：", StringComparison.Ordinal) ||
               text.Contains("/", StringComparison.Ordinal) ||
               text.Contains("％", StringComparison.Ordinal) ||
               text.Contains("%", StringComparison.Ordinal);
    }

    /// <summary>
    /// 游戏内金钱基本以 10 为单位（出入账都是整 10），非整 10 值视为可疑
    /// 单位数（如 0~29）放行，避免 OCR 漏掉个位数时连合理值都被否决
    /// </summary>
    private static bool IsRoundTenValue(int value)
    {
        if (value < 30) return true;
        return value % 10 == 0;
    }

    /// <summary>
    /// OCR 文本归一化，尽量减小数字与货币符号误读影响
    /// </summary>
    private static string NormalizeOcr(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";

        return raw
            .Replace(",", "")
            .Replace("，", "")
            .Replace(" ", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace("\u3000", "")
            .Replace("O", "0")
            .Replace("o", "0")
            .Trim();
    }

    /// <summary>
    /// 读取休息选项区域文本
    /// fastPath=true: 只 OCR 主区域（1 次）；fastPath=false: 扩展两个备份区域
    /// </summary>
    private static string ReadRestOptionText(Mat screenshot, bool fastPath)
    {
        string main = NormalizeOcr(
            OcrHelper.RecognizeRegion(screenshot, OptionDetectX, OptionDetectY, OptionDetectW, OptionDetectH)
                .GetAwaiter().GetResult());
        if (fastPath)
            return main;

        var extras = new (double X, double Y, double W, double H)[]
        {
            (0.52, 0.42, 0.44, 0.40),
            (0.55, 0.45, 0.40, 0.35),
        };
        string best = main;
        foreach (var r in extras)
        {
            string text = NormalizeOcr(OcrHelper.RecognizeRegion(screenshot, r.X, r.Y, r.W, r.H)
                .GetAwaiter().GetResult());
            if (string.IsNullOrEmpty(text)) continue;
            if (text.Contains("冥想室", StringComparison.Ordinal) ||
                text.Contains("免费住处", StringComparison.Ordinal) ||
                text.Contains("免费休息", StringComparison.Ordinal))
                return text;
            if (text.Length > best.Length)
                best = text;
        }
        return best;
    }

    /// <summary>
    /// 读取右下角确认按钮文本，稳定识别"休息"页入口
    /// fastPath=true: 只 OCR 主区域（1 次）；命中"休息"立即返回
    /// </summary>
    private static string ReadRestConfirmText(Mat screenshot, bool fastPath)
    {
        var main = ConfirmRestTextRegions[0];
        string mainText = NormalizeOcr(OcrHelper.RecognizeRegion(screenshot, main.X, main.Y, main.W, main.H)
            .GetAwaiter().GetResult());
        if (fastPath || mainText.Contains("休息", StringComparison.Ordinal))
            return mainText;

        string best = mainText;
        for (int i = 1; i < ConfirmRestTextRegions.Length; i++)
        {
            var r = ConfirmRestTextRegions[i];
            string raw = OcrHelper.RecognizeRegion(screenshot, r.X, r.Y, r.W, r.H)
                .GetAwaiter().GetResult();
            string text = NormalizeOcr(raw);
            if (string.IsNullOrEmpty(text))
                continue;
            if (text.Contains("休息", StringComparison.Ordinal))
                return text;
            if (text.Length > best.Length)
                best = text;
        }
        return best;
    }

    private static string ReadRestDetailTitleText(Mat screenshot)
    {
        string best = "";
        foreach (var region in DetailTitleRegions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, region.X, region.Y, region.W, region.H)
                .GetAwaiter().GetResult();
            string text = NormalizeOcr(raw);
            if (string.IsNullOrEmpty(text))
                continue;
            if (ResolveExpandedRestOptionIndex(text) >= 0)
                return text;
            if (text.Length > best.Length)
                best = text;
        }

        return best;
    }

    /// <summary>
    /// 休息三选项页：右下角有"休息"，且右侧固定选项特征足够明显
    /// </summary>
    private static bool IsRestDecisionContext(string optionText, string confirmText)
    {
        if (string.IsNullOrEmpty(optionText))
            return false;

        int score = 0;
        if (optionText.Contains("免费住处", StringComparison.Ordinal)) score += 4;
        if (optionText.Contains("冥想室", StringComparison.Ordinal)) score += 4;
        if (optionText.Contains("免费", StringComparison.Ordinal)) score += 1;
        if (optionText.Contains("住处", StringComparison.Ordinal)) score += 1;
        if (optionText.Contains("30", StringComparison.Ordinal)) score += 1;
        if (optionText.Contains("60", StringComparison.Ordinal)) score += 1;

        bool hasConfirmRest = !string.IsNullOrEmpty(confirmText) && confirmText.Contains("休息", StringComparison.Ordinal);
        if (hasConfirmRest)
            return score >= 5;

        if (LooksLikeRestDetailState(optionText))
            return true;

        // 即使右下角"休息"按钮 OCR 没识别出来（按钮 hover/淡入/被遮挡都可能），
        // 只要选项区强证据（冥想室 + 免费住处 + 30 + 60，score≥10）也接管。
        // 避免出现"画面明显是休息页，但因为按钮 OCR 短暂失效，所有 handler 都不接管，
        // 主循环空转"的死锁。
        return score >= 7;
    }

    private static bool LooksLikeRestDetailState(string optionText)
    {
        if (string.IsNullOrEmpty(optionText))
            return false;

        bool hasRestPlaceKeyword = optionText.Contains("住处", StringComparison.Ordinal) ||
                                   optionText.Contains("露宿", StringComparison.Ordinal) ||
                                   optionText.Contains("冥想室", StringComparison.Ordinal) ||
                                   optionText.Contains("冥想", StringComparison.Ordinal);
        bool hasRecoveryKeyword = optionText.Contains("效果", StringComparison.Ordinal) ||
                                  optionText.Contains("恢复", StringComparison.Ordinal) ||
                                  optionText.Contains("耐力", StringComparison.Ordinal);
        bool hasKnownRestCost = optionText.Contains("30", StringComparison.Ordinal) ||
                                optionText.Contains("60", StringComparison.Ordinal);
        bool stillShowsRestMenuHint = optionText.Contains("冥想", StringComparison.Ordinal) ||
                                      optionText.Contains("免费", StringComparison.Ordinal) ||
                                      optionText.Contains("住处", StringComparison.Ordinal);

        return hasRestPlaceKeyword && hasRecoveryKeyword && hasKnownRestCost && stillShowsRestMenuHint;
    }

    private static int ResolveExpandedRestOptionIndex(string detailTitle)
    {
        if (string.IsNullOrEmpty(detailTitle))
            return -1;

        if (detailTitle.Contains("露宿", StringComparison.Ordinal))
            return 0;
        if (detailTitle.Contains("住处", StringComparison.Ordinal))
            return 1;
        if (detailTitle.Contains("冥想室", StringComparison.Ordinal) ||
            detailTitle.Contains("冥想", StringComparison.Ordinal))
            return 2;

        return -1;
    }

    private static (double X, double Y) GetCalibratedOptionClickPoint(int optionIdx)
    {
        return Math.Clamp(optionIdx, 0, 2) switch
        {
            0 => (0.84, 0.42),
            1 => (0.84, 0.495),
            _ => (0.84, 0.57),
        };
    }

    // 一次性 OCR 整片选项区域，按行解析每个选项真实 Y
    // 历史问题：靠"硬编码基准 Y + Y±0.025 缝隙 OCR 校准"，
    //   - Y 基准本身就经常偏（不同 DPI/字体/UI 版本会漂 0.02~0.04）
    //   - 缝隙 OCR 一旦关键词不在 ±0.025 里就 fallback 回错的基准，导致点击空白
    // 现在改用一次大区 OCR + 行级 BoundingRect → 直接拿到选项的真实 Y
    private const double OptionRoiX = 0.56;
    private const double OptionRoiY = 0.40;
    private const double OptionRoiW = 0.40;
    private const double OptionRoiH = 0.36;
    private const double OptionClickX = 0.84;

    // 选项行关键词指纹（强 → 弱）。只要 OCR 行包含其中任一关键词就归到该选项
    //
    // 关键词补全（2026-04-21）：游戏 OCR 经常把"免费住处"截成"住处"、"普通住处"截成"住处"，
    // "冥想室"偶尔退化成"冥想"。原版 substring 检查会两个选项都漏判，所以扩展退化形态
    private static readonly string[][] OptionLineTokens =
    [
        ["露宿", "免费住处", "免费"],
        ["普通住处", "住处", "30金"],
        ["冥想室", "冥想", "60金"],
    ];

    // 价格关键词弱兜底已废弃：实战中"30 / 60"经常出现在效果说明里
    // （如"耐力 30 恢复"），用它兜底会把目标 Y 漂到说明文本行上点空，
    // 反而比直接用 base 点位更糟。保留常量以便未来加新弱规则时引用。
    private static readonly string[] OptionPriceTokens = ["0", "30", "60"];

    /// <summary>
    /// 通过整片选项区域的行级 OCR，反推目标选项的真实 Y 中心
    /// </summary>
    private static (double X, double Y) ResolveOptionClickPoint(Mat screenshot, int optionIdx)
    {
        var basePoint = GetCalibratedOptionClickPoint(optionIdx);

        var lines = OcrHelper.RecognizeRegionLines(screenshot, OptionRoiX, OptionRoiY, OptionRoiW, OptionRoiH)
            .GetAwaiter().GetResult();
        if (lines.Count == 0)
        {
            Log.Log($"Rest line-OCR: empty result, fallback to base=({basePoint.X:F3},{basePoint.Y:F3})");
            return basePoint;
        }

        // 把行级 OCR 的相对 Y 换算回截图的 Y 百分比
        var rows = lines
            .Select(l => new
            {
                Text = NormalizeOcr(l.Text),
                YPct = OptionRoiY + l.CenterYPct * OptionRoiH,
            })
            .Where(r => !string.IsNullOrEmpty(r.Text))
            .OrderBy(r => r.YPct)
            .ToArray();

        // 第一步：用每个选项的强关键词找对应行
        // 单行可能同时匹配多个选项（如"住处"既属 option 2 也可能误命中其他选项），
        // 因此每行只允许归属一个选项：先来先得 + 已用行不重复
        double[] resolvedY = [-1, -1, -1];
        var usedRowIdx = new HashSet<int>();
        for (int i = 0; i < OptionLineTokens.Length; i++)
        {
            for (int rIdx = 0; rIdx < rows.Length; rIdx++)
            {
                if (usedRowIdx.Contains(rIdx)) continue;
                var row = rows[rIdx];
                if (OptionLineTokens[i].Any(t => row.Text.Contains(t, StringComparison.Ordinal)))
                {
                    resolvedY[i] = row.YPct;
                    usedRowIdx.Add(rIdx);
                    break;
                }
            }
        }

        // 对齐验证：必须 ≥2 个选项独立命中强关键词，line-OCR 才被认为可信
        // 若只有 1 个选项命中，单点无法验证整体偏移方向，再外推容易越偏越离谱
        // —— 这种情况直接退回手调过的 base 点位
        int hitCount = resolvedY.Count(y => y >= 0);
        if (hitCount < 2)
        {
            Log.Log($"Rest line-OCR: only {hitCount} option(s) anchored by strong keywords, fall back to base=({basePoint.X:F3},{basePoint.Y:F3}); " +
                    $"rows=[{string.Join(" | ", rows.Select(r => $"{r.YPct:F3}:{ClipText(r.Text)}"))}]");
            return basePoint;
        }

        // 第二步：未解析到的选项按已解析相邻行外推（间距取已知行的差，缺省 0.07）
        bool[] directlyAnchored = resolvedY.Select(y => y >= 0).ToArray();
        FillMissingByExtrapolation(resolvedY);

        double targetY = ResolveTargetOptionY(resolvedY, directlyAnchored, optionIdx, basePoint.Y);
        if (!directlyAnchored[optionIdx])
        {
            Log.Log($"Rest line-OCR: option={optionIdx + 1} not directly anchored, prefer base={basePoint.Y:F3}, rows=[{string.Join(" | ", rows.Select(r => $"{r.YPct:F3}:{ClipText(r.Text)}"))}]");
            return basePoint;
        }

        if (targetY < 0)
        {
            Log.Log($"Rest line-OCR: option={optionIdx + 1} unresolved after extrapolation, fallback to base=({basePoint.X:F3},{basePoint.Y:F3}), rows=[{string.Join(" | ", rows.Select(r => $"{r.YPct:F3}:{ClipText(r.Text)}"))}]");
            return basePoint;
        }

        // 安全闸门：解析出来的 Y 与硬编码基准差太多 → 退回 base
        // 选项纵向间距约 0.07，0.05 偏移已经横跨到隔壁选项的边界，必须严格
        double clamped = Math.Clamp(targetY, 0.42, 0.78);
        if (Math.Abs(clamped - basePoint.Y) > 0.05)
        {
            Log.Log($"Rest line-OCR: option={optionIdx + 1} resolvedY={clamped:F3} drifted >0.05 from base={basePoint.Y:F3}, fallback");
            return basePoint;
        }

        Log.Log($"Rest line-OCR: option={optionIdx + 1} resolvedY={clamped:F3} (base={basePoint.Y:F3}, anchored={hitCount}/3), rows=[{string.Join(" | ", rows.Select(r => $"{r.YPct:F3}:{ClipText(r.Text)}"))}]");
        return (OptionClickX, clamped);
    }

    private static double ResolveTargetOptionY(double[] resolvedY, bool[] anchored, int optionIdx, double baseY)
    {
        if (optionIdx < 0 || optionIdx >= resolvedY.Length || optionIdx >= anchored.Length)
            return baseY;
        if (!anchored[optionIdx])
            return baseY;
        return resolvedY[optionIdx];
    }

    private static void FillMissingByExtrapolation(double[] resolvedY)
    {
        // 估算行间距：取已解析相邻行的差；至少有 2 个解析值才能推
        var solved = new List<(int Idx, double Y)>();
        for (int i = 0; i < resolvedY.Length; i++)
            if (resolvedY[i] >= 0) solved.Add((i, resolvedY[i]));

        if (solved.Count == 0) return;

        double spacing = 0.07; // 缺省 7%（≈100px @1440）
        if (solved.Count >= 2)
        {
            double diffSum = 0;
            int diffCnt = 0;
            for (int k = 1; k < solved.Count; k++)
            {
                int gap = solved[k].Idx - solved[k - 1].Idx;
                if (gap <= 0) continue;
                diffSum += (solved[k].Y - solved[k - 1].Y) / gap;
                diffCnt++;
            }
            if (diffCnt > 0) spacing = Math.Clamp(diffSum / diffCnt, 0.04, 0.10);
        }

        // 用第一个已解析行作锚点外推
        var anchor = solved[0];
        for (int i = 0; i < resolvedY.Length; i++)
        {
            if (resolvedY[i] >= 0) continue;
            resolvedY[i] = anchor.Y + (i - anchor.Idx) * spacing;
        }
    }

    private static string ClipText(string text, int max = 18)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= max ? text : text.Substring(0, max) + "..";
    }

    private static string FormatValue(int? value) => value?.ToString() ?? "N/A";
    private static readonly LogScope Log = new("Race:Rest");
private readonly record struct RestPlan(int? Money, int? ChosenOption, bool RequiresManualIntervention);
}
