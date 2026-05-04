using System.Text.RegularExpressions;
using OpenCvSharp;
using SleepRunner.Recognition;

namespace SleepRunner.Automation.Race.Handlers.Events;

/// <summary>
/// 事件页所有"判定一段 OCR 文本属于什么页"的静态识别函数 + 文本计数工具
///
/// 拆分意图：
/// - EventHandler 的 CanHandle 大量使用 IsXxx 系列做"是不是事件页"的兜底
/// - 集中后修一个误判（如 IsRestDecisionContext / IsMainMenuLikeText 阈值）只动这里
/// - IsRetryDialogContext 内联 OCR 调用以避免和 EventOcrRegions 的非循环耦合
/// </summary>
internal static class EventScreenChecks
{
    /// <summary>
    /// 检测当前是否为"战斗失败 - 重新挑战通知"弹窗，是的话 EventHandler 应让出
    /// 与 BattleDefeatHandler.IsRetryDialog 区域/关键词保持一致
    /// </summary>
    public static bool IsRetryDialogContext(Mat screenshot)
    {
        const double dialogX = 0.20;
        const double dialogY = 0.15;
        const double dialogW = 0.60;
        const double dialogH = 0.30;
        string raw = OcrHelper.RecognizeRegion(screenshot, dialogX, dialogY, dialogW, dialogH)
            .GetAwaiter()
            .GetResult();
        string text = EventOcrRegions.NormalizeOcr(raw);
        if (string.IsNullOrEmpty(text)) return false;
        if (text.Contains("重新挑战通知", StringComparison.Ordinal)) return true;
        if (text.Contains("是否要重新挑战", StringComparison.Ordinal)) return true;
        if (text.Contains("再次尝试战斗", StringComparison.Ordinal)) return true;
        return false;
    }

    public static bool IsJourneyEventMarker(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (text.Contains("旅程事件", StringComparison.Ordinal)) return true;
        return text.Contains("旅程", StringComparison.Ordinal) &&
               text.Contains("事件", StringComparison.Ordinal);
    }

    public static bool ContainsJourneyHint(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("旅程", StringComparison.Ordinal) ||
               text.Contains("事件", StringComparison.Ordinal) ||
               text.Contains("距离目标", StringComparison.Ordinal) ||
               text.Contains("距离目", StringComparison.Ordinal) ||
               (text.Contains("评鉴战", StringComparison.Ordinal) && text.Contains("胜利", StringComparison.Ordinal));
    }

    /// <summary>
    /// 过滤左上角时间轴噪声（如"4月下旬"）避免覆盖事件标识判定
    /// </summary>
    public static bool IsJourneyNoise(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return Regex.IsMatch(text, @"\d{1,2}月(上旬|中旬|下旬)") ||
               Regex.IsMatch(text, @"0\d月(上旬|中旬|下旬)") ||
               text.Contains("月上旬", StringComparison.Ordinal) ||
               text.Contains("月中旬", StringComparison.Ordinal) ||
               text.Contains("月下旬", StringComparison.Ordinal);
    }

    /// <summary>
    /// 判断文本是否像"需要玩家选择"的事件选项提示
    /// </summary>
    public static bool IsEventOptionHint(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (IsMainMenuLikeText(text)) return false;
        if (IsAppraisePrepareSheetText(text)) return false;
        if (text.Count(c => c == '+') >= 2)
            return true;
        if (text.Contains("该怎么办", StringComparison.Ordinal) ||
            text.Contains("怎么办", StringComparison.Ordinal))
            return true;
        if (text.Contains("请选择", StringComparison.Ordinal) ||
            text.Contains("选项", StringComparison.Ordinal))
            return true;
        if (text.Contains("是否", StringComparison.Ordinal) ||
            text.Contains("要不要", StringComparison.Ordinal))
            return true;

        int sentenceCount = text.Count(c => c == '。');
        if (sentenceCount >= 2 && CountChineseChars(text) >= 12)
            return true;
        if ((text.Contains("左边", StringComparison.Ordinal) || text.Contains("右边", StringComparison.Ordinal) ||
             text.Contains("左侧", StringComparison.Ordinal) || text.Contains("右侧", StringComparison.Ordinal)) &&
            (text.Contains("箱子", StringComparison.Ordinal) || text.Contains("盒子", StringComparison.Ordinal)))
            return true;
        if (text.Contains("等待下次机会", StringComparison.Ordinal) ||
            text.Contains("下次机会", StringComparison.Ordinal))
            return true;

        int zhCount = CountChineseChars(text);
        if (zhCount >= 8 &&
            (text.Contains('？') || text.Contains('?') || text.Contains('呢') || text.Contains('吧')))
            return true;

        return false;
    }

    private static bool IsAppraisePrepareSheetText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        bool hasAppraise = text.Contains("评鉴战", StringComparison.Ordinal);
        bool hasPrepare = text.Contains("战前准备", StringComparison.Ordinal) ||
                          text.Contains("即将开始", StringComparison.Ordinal);
        bool hasBattleSheet = text.Contains("建议综合等级", StringComparison.Ordinal) ||
                              text.Contains("登场敌人", StringComparison.Ordinal) ||
                              text.Contains("可获得奖励", StringComparison.Ordinal);
        return hasAppraise && hasPrepare && hasBattleSheet;
    }

    /// <summary>
    /// 判断是否命中"列车月台单选界面"文本线索
    /// </summary>
    public static bool IsTrainPlatformSingleOptionScreen(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        bool hasPlatform = text.Contains("月台", StringComparison.Ordinal) ||
                           text.Contains("站台", StringComparison.Ordinal);
        bool hasTrain = text.Contains("列车", StringComparison.Ordinal) ||
                        text.Contains("车站", StringComparison.Ordinal) ||
                        text.Contains("轨道", StringComparison.Ordinal);
        if (text.Contains("列车月台", StringComparison.Ordinal))
            return true;
        return hasPlatform && hasTrain;
    }

    /// <summary>
    /// 识别评鉴战的"胜利目标奖励列表"页，避免 EventHandler 误把 +10/+20 奖励当成事件选项
    ///
    /// 典型 OCR：'30回合以内获胜（19/30）25回合以内获胜（19/25）20回合以内获胜（19/20）+10+10+10'
    /// 指纹：
    /// - 含 "回合以内获胜" / "回合内获胜" 等评鉴战目标短语（事件文本基本不会出现这串）
    /// - 或者出现 ≥2 个形如 "数字/数字" 的进度分数对
    /// </summary>
    public static bool IsAppraiseGoalListContext(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        if (text.Contains("回合以内获胜", StringComparison.Ordinal) ||
            text.Contains("回合内获胜", StringComparison.Ordinal) ||
            text.Contains("回合以内取胜", StringComparison.Ordinal))
            return true;

        // 进度分数对（如 19/30、19/25）出现 ≥2 次也是评鉴战奖励列表的强指纹
        int progressPairs = System.Text.RegularExpressions.Regex.Matches(text, @"\d{1,3}/\d{1,3}").Count;
        if (progressPairs >= 2)
            return true;

        return false;
    }

    /// <summary>
    /// 过滤主菜单/路由文案，避免事件兜底误触发
    /// </summary>
    public static bool IsMainMenuLikeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        int menuKeywordCount = CountMainMenuKeywords(text);
        bool hasCommissionRoute = text.Contains("委托", StringComparison.Ordinal) ||
                                  text.Contains("讨伐", StringComparison.Ordinal) ||
                                  text.Contains("受理", StringComparison.Ordinal);
        if (hasCommissionRoute && menuKeywordCount >= 2)
            return true;

        if (text.Contains('+') ||
            text.Contains('。') ||
            text.Contains('！') ||
            text.Contains('？') ||
            text.Contains('?') ||
            text.Contains('，'))
            return false;

        int zhCount = CountChineseChars(text);
        if (zhCount > 12)
            return false;

        bool hasCommission = text.Contains("委托", StringComparison.Ordinal);
        bool hasRest = text.Contains("休息", StringComparison.Ordinal);
        bool hasTrain = text.Contains("训练", StringComparison.Ordinal);
        return (hasCommission && hasRest) || (hasTrain && hasRest);
    }

    /// <summary>
    /// 识别休息三选项页，避免 EventHandler 抢走 RestDecisionHandler 的入口
    ///
    /// 设计要点（2026-04-21 放宽）：
    /// - 不再硬性要求 confirmText 包含"休息"。右下角按钮 OCR 经常因 hover/淡入失效，
    ///   一旦 confirm 没读到就把整个判定枪毙等于把控制权拱手让给 EventHandler 兜底
    /// - 允许 OCR 退化：「冥想」常被读成无"室"、「免费住处」常被拆成单独的"免费"+"住处"
    /// - 价格 30 + 60 同现是休息页最稳定的指纹（事件文本里几乎不会同时出现两个独立两位价格）
    /// </summary>
    public static bool IsRestDecisionContext(string optionText, string confirmText)
    {
        if (string.IsNullOrEmpty(optionText)) return false;

        int score = 0;
        // 强关键词
        if (optionText.Contains("免费住处", StringComparison.Ordinal)) score += 4;
        if (optionText.Contains("冥想室", StringComparison.Ordinal)) score += 4;
        // OCR 退化兜底
        if (optionText.Contains("冥想", StringComparison.Ordinal)) score += 2;
        if (optionText.Contains("免费", StringComparison.Ordinal)) score += 1;
        if (optionText.Contains("住处", StringComparison.Ordinal)) score += 1;
        if (optionText.Contains("露宿", StringComparison.Ordinal)) score += 1;
        if (optionText.Contains("30", StringComparison.Ordinal)) score += 1;
        if (optionText.Contains("60", StringComparison.Ordinal)) score += 1;
        // 价格双现 = 休息页强指纹
        if (optionText.Contains("30", StringComparison.Ordinal) &&
            optionText.Contains("60", StringComparison.Ordinal))
            score += 3;

        bool hasConfirmRest = !string.IsNullOrEmpty(confirmText) &&
                              confirmText.Contains("休息", StringComparison.Ordinal);
        // confirm 命中时门槛低；confirm 没读到时要求更强证据，但仍允许接管
        return hasConfirmRest ? score >= 4 : score >= 7;
    }

    /// <summary>
    /// 统计 OCR 文本里出现的主菜单/D-DAY 菜单关键词数量
    /// ≥2 即可判定为主菜单画面
    /// </summary>
    public static int CountMainMenuKeywords(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int count = 0;
        if (text.Contains("训练", StringComparison.Ordinal)) count++;
        if (text.Contains("委托", StringComparison.Ordinal)) count++;
        if (text.Contains("休息", StringComparison.Ordinal)) count++;
        if (text.Contains("评鉴战", StringComparison.Ordinal)) count++;
        if (text.Contains("交易", StringComparison.Ordinal)) count++;
        if (text.Contains("出击", StringComparison.Ordinal)) count++;
        return count;
    }

    /// <summary>
    /// 给"哪个 ROI 读到的事件标题更可信"打分
    /// </summary>
    public static int ScoreEventTitleText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return -10;
        if (IsJourneyNoise(text))
            return -20;

        int score = CountChineseChars(text) * 2;
        if (text.Contains("旅程事件", StringComparison.Ordinal)) score += 8;
        if (text.Contains("天气", StringComparison.Ordinal)) score += 8;
        if (text.Contains("浓雾", StringComparison.Ordinal) || text.Contains("大雾", StringComparison.Ordinal)) score += 6;
        if (text.Contains("雷雨", StringComparison.Ordinal)) score += 6;
        if (text.Contains("距离目标", StringComparison.Ordinal) || text.Contains("评鉴战胜利", StringComparison.Ordinal)) score -= 16;
        if (text.Length >= 4 && text.Length <= 20) score += 4;
        return score;
    }

    /// <summary>
    /// 截短日志预览，避免 OCR 长文本污染日志
    /// </summary>
    public static string ClipPreview(string text, int max = 60)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length <= max) return text;
        return text.Substring(0, max) + "...";
    }

    /// <summary>
    /// 统计文本中的中文字符数量，用于弱特征兜底
    /// </summary>
    public static int CountChineseChars(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int count = 0;
        foreach (char c in text)
        {
            if (c >= 0x4E00 && c <= 0x9FFF)
                count++;
        }
        return count;
    }

    /// <summary>
    /// 过滤训练详细页/训练分流页，避免误判为事件选择
    /// </summary>
    public static bool IsTrainingContext(string marker, string optionText)
    {
        string m = marker ?? "";
        string o = optionText ?? "";
        bool markerHit = m.Contains("距离目标评鉴战", StringComparison.Ordinal) ||
                         m.Contains("目标评鉴战", StringComparison.Ordinal) ||
                         m.Contains("训练", StringComparison.Ordinal);
        bool optionHit = o.Contains("训练Lv", StringComparison.OrdinalIgnoreCase) ||
                         (o.Contains("训练", StringComparison.Ordinal) &&
                          (o.Contains("力量", StringComparison.Ordinal) ||
                           o.Contains("体力", StringComparison.Ordinal) ||
                           o.Contains("韧性", StringComparison.Ordinal) ||
                           o.Contains("集中", StringComparison.Ordinal) ||
                           o.Contains("专注", StringComparison.Ordinal) ||
                           o.Contains("保护", StringComparison.Ordinal)));
        return markerHit && optionHit;
    }

}
