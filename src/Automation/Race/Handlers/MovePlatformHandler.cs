using OpenCvSharp;
using System.Linq;
using System.Text.RegularExpressions;
using SleepRunner.Automation.Race.Policy;
using SleepRunner.Utils;
using SleepRunner.Recognition;
using SleepRunner.Vision;

namespace SleepRunner.Automation.Race.Handlers;

/// <summary>
/// 地区移动单选页处理：右侧命中“列车月台”时固定选择第一个选项
/// </summary>
public class MovePlatformHandler : IRaceHandler
{
    public string Name => "地区移动月台";
    public int Priority => 4;

    // 左上角阶段标题区域（用于限定只在地区移动页触发）
    private const double TitleX = 0.00;
    private const double TitleY = 0.04;
    private const double TitleW = 0.30;
    private const double TitleH = 0.16;

    // 右上标题候选区域（重点识别“列车月台”）
    private static readonly (double X, double Y, double W, double H)[] PlatformHeaderRegions =
    [
        (0.74, 0.10, 0.24, 0.14),
        (0.72, 0.08, 0.26, 0.16),
        (0.76, 0.12, 0.22, 0.12),
    ];

    // 右侧单选框候选区域（识别站点名）
    private static readonly (double X, double Y, double W, double H)[] RightOptionRegions =
    [
        (0.74, 0.22, 0.24, 0.16),
        (0.72, 0.20, 0.26, 0.20),
        (0.76, 0.24, 0.22, 0.14),
    ];

    // 单选页点击第一个选项（右侧站点框中心）
    private const double FirstOptionX = 0.86;
    private const double FirstOptionY = 0.30;
    private const double SecondOptionY = 0.42;
    // 右侧候选行探针：用于判断是单选还是双选
    private static readonly (double Y, double X, double W, double H)[] OptionLineProbes =
    [
        (0.30, 0.74, 0.24, 0.10),
        (0.42, 0.74, 0.24, 0.10),
    ];
    // 右下“前往”按钮区域与默认点击点
    private const double GoButtonRegionX = 0.76;
    private const double GoButtonRegionY = 0.84;
    private const double GoButtonRegionW = 0.22;
    private const double GoButtonRegionH = 0.12;
    private const double GoButtonX = 0.88;
    private const double GoButtonY = 0.91;
    private static readonly (double X, double Y, double W, double H)[] GoButtonScanRegions =
    [
        (0.76, 0.84, 0.22, 0.06),
        (0.76, 0.88, 0.22, 0.06),
        (0.76, 0.92, 0.22, 0.06),
    ];

    public MovePlatformHandler()
    {
    }

    /// <summary>
    /// 判断当前界面是否为“地区移动-列车月台单选页”
    /// </summary>
    public bool CanHandle(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        string title = ReadTitleText(screenshot);
        if (!IsMoveStageTitle(title))
            return false;

        string headerText = ReadPlatformHeaderText(screenshot);
        string rightText = ReadRightOptionText(screenshot);
        bool hit = IsPlatformHeaderText(headerText) || IsTrainPlatformText(rightText);
        if (hit)
        {
            Log.Log($"Move platform hit: title='{title}', header='{headerText}', right='{rightText}'");
            return true;
        }

        if (!string.IsNullOrEmpty(headerText) || !string.IsNullOrEmpty(rightText))
            Log.Log($"Move platform miss: title='{title}', header='{headerText}', right='{rightText}'");
        return false;
    }

    /// <summary>
    /// 输出单步决策预览
    /// </summary>
    public string DescribeDecision(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        string headerText = ReadPlatformHeaderText(screenshot);
        string rightText = ReadRightOptionText(screenshot);
        var (count, clickY, reason) = ResolveOptionPick(screenshot);
        int optionIndex = Math.Abs(clickY - SecondOptionY) < 0.02 ? 2 : 1;
        return $"Move stage: header='{headerText}', right='{rightText}' -> options={count}, pick option {optionIndex} ({reason}) at ({FirstOptionX:F2},{clickY:F3})";
    }

    /// <summary>
    /// 执行地区移动单选点击
    /// </summary>
    public async Task HandleAsync(GameContext ctx)
    {
        using var shot = ctx.CaptureScreen();
        if (shot == null || shot.Empty())
            return;

        string title = ReadTitleText(shot);
        string headerText = ReadPlatformHeaderText(shot);
        string rightText = ReadRightOptionText(shot);
        bool hit = IsPlatformHeaderText(headerText) || IsTrainPlatformText(rightText);
        if (!IsMoveStageTitle(title) || !hit)
        {
            Log.Log($"Skip execute: title='{title}', header='{headerText}', right='{rightText}'");
            return;
        }

        // 第一步：按选项数量+基调决定点击行
        var (optionCount, pickY, reason) = ResolveOptionPick(shot);
        int optionIndex = Math.Abs(pickY - SecondOptionY) < 0.02 ? 2 : 1;
        Log.Log($"Execute move platform: options={optionCount}, pick option {optionIndex} ({reason}) at ({FirstOptionX:F2},{pickY:F3}).");
        await ctx.ClickAtPercent(FirstOptionX, pickY);
        await ctx.Wait(700);

        // 第二步：检测并点击“前往”按钮，推进到下一步
        using var afterPick = ctx.CaptureScreen();
        if (afterPick == null || afterPick.Empty())
            return;

        var goClick = ResolveGoButtonClickPoint(afterPick, out string goText, out bool hitGoText);
        if (hitGoText)
        {
            Log.Log($"Go button detected ('{goText}'), clicking go at ({goClick.X:F2},{goClick.Y:F3}).");
            await ctx.ClickAtPercent(goClick.X, goClick.Y);
            await ctx.Wait(1500);
            return;
        }

        Log.Log($"Go button miss after option click, text='{goText}', fallback click go at ({goClick.X:F2},{goClick.Y:F3}).");
        await ctx.ClickAtPercent(goClick.X, goClick.Y);
        await ctx.Wait(1500);
    }

    /// <summary>
    /// 读取左上角标题文本
    /// </summary>
    private static string ReadTitleText(Mat screenshot)
    {
        string raw = OcrHelper.RecognizeRegion(screenshot, TitleX, TitleY, TitleW, TitleH)
            .GetAwaiter()
            .GetResult();
        return Normalize(raw);
    }

    /// <summary>
    /// 读取右侧选项文本，优先返回命中月台特征的结果
    /// </summary>
    private static string ReadRightOptionText(Mat screenshot)
    {
        string best = "";
        foreach (var region in RightOptionRegions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, region.X, region.Y, region.W, region.H)
                .GetAwaiter()
                .GetResult();
            string text = Normalize(raw);
            if (string.IsNullOrEmpty(text))
                continue;

            if (IsTrainPlatformText(text))
                return text;
            if (text.Length > best.Length)
                best = text;
        }

        return best;
    }

    /// <summary>
    /// 读取右上“列车月台”标题文本
    /// </summary>
    private static string ReadPlatformHeaderText(Mat screenshot)
    {
        string best = "";
        foreach (var region in PlatformHeaderRegions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, region.X, region.Y, region.W, region.H)
                .GetAwaiter()
                .GetResult();
            string text = Normalize(raw);
            if (string.IsNullOrEmpty(text))
                continue;
            if (IsPlatformHeaderText(text))
                return text;
            if (text.Length > best.Length)
                best = text;
        }

        return best;
    }

    /// <summary>
    /// 判断标题是否为地区移动阶段
    /// </summary>
    private static bool IsMoveStageTitle(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        return text.Contains("地区移动", StringComparison.Ordinal) ||
               text.Contains("目标地区移动", StringComparison.Ordinal);
    }

    /// <summary>
    /// 判断右侧文本是否命中“列车月台”语义（包含 OCR 容错）
    /// </summary>
    private static bool IsTrainPlatformText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        if (text.Contains("列车月台", StringComparison.Ordinal))
            return true;
        if (text.Contains("车月台", StringComparison.Ordinal) ||
            text.Contains("车站台", StringComparison.Ordinal))
            return true;
        if (text.Contains("列车", StringComparison.Ordinal) &&
            (text.Contains("月台", StringComparison.Ordinal) || text.Contains("站台", StringComparison.Ordinal)))
            return true;

        // OCR 断裂容错：例如“列 车 月 台”
        return Regex.IsMatch(text, "(列|刂)?.?车.*(月.?台|站.?台)");
    }

    /// <summary>
    /// 判断是否命中“列车月台”标题文本（仅看右上标题）
    /// </summary>
    private static bool IsPlatformHeaderText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        if (text.Contains("列车月台", StringComparison.Ordinal))
            return true;
        if (text.Contains("车月台", StringComparison.Ordinal))
            return true;
        return (text.Contains("列车", StringComparison.Ordinal) && text.Contains("月台", StringComparison.Ordinal)) ||
               Regex.IsMatch(text, "(列|刂)?.?车.*月.?台");
    }

    /// <summary>
    /// 判断当前是单选还是双选，并按基调返回点击行
    /// </summary>
    private static (int Count, double PickY, string Reason) ResolveOptionPick(Mat screenshot)
    {
        int hitCount = 0;
        string[] texts = new string[OptionLineProbes.Length];
        for (int i = 0; i < OptionLineProbes.Length; i++)
        {
            var p = OptionLineProbes[i];
            string raw = OcrHelper.RecognizeRegion(screenshot, p.X, p.Y - p.H / 2, p.W, p.H)
                .GetAwaiter()
                .GetResult();
            string text = Normalize(raw);
            texts[i] = text;
            if (IsOptionLineText(text))
                hitCount++;
        }

        // 双选规则：攻击基调选第一个，非攻击基调选第二个
        if (hitCount >= 2)
        {
            int optionIndex = EventProfileSettings.MovePlatformOptionIndex;
            if (optionIndex <= 1)
                return (2, FirstOptionY, $"two-options profile-first line1='{texts[0]}' line2='{texts[1]}'");
            return (2, SecondOptionY, $"two-options profile-second line1='{texts[0]}' line2='{texts[1]}'");
        }

        return (1, FirstOptionY, $"single-option line1='{texts[0]}' line2='{texts[1]}'");
    }

    /// <summary>
    /// 判断一行文本是否像月台选项文本
    /// </summary>
    private static bool IsOptionLineText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        int zh = text.Count(c => c >= 0x4E00 && c <= 0x9FFF);
        if (zh < 2)
            return false;
        if (text.Contains("前往", StringComparison.Ordinal) ||
            text.Contains("返回", StringComparison.Ordinal))
            return false;
        return true;
    }

    /// <summary>
    /// 判断右下是否出现“前往”按钮文案
    /// </summary>
    private static bool HasGoButton(Mat screenshot, out string normalizedText)
    {
        string raw = OcrHelper.RecognizeRegion(
                screenshot,
                GoButtonRegionX,
                GoButtonRegionY,
                GoButtonRegionW,
                GoButtonRegionH)
            .GetAwaiter()
            .GetResult();
        normalizedText = Normalize(raw);
        return normalizedText.Contains("前往", StringComparison.Ordinal) ||
               normalizedText.Contains("前", StringComparison.Ordinal);
    }

    /// <summary>
    /// 扫描“前往”按钮候选行，返回更贴近文本位置的点击点
    /// </summary>
    private static (double X, double Y) ResolveGoButtonClickPoint(
        Mat screenshot,
        out string bestText,
        out bool hitGoText)
    {
        bestText = "";
        hitGoText = false;
        double bestX = GoButtonX;
        double bestY = GoButtonY;
        int bestScore = int.MinValue;

        foreach (var region in GoButtonScanRegions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, region.X, region.Y, region.W, region.H)
                .GetAwaiter()
                .GetResult();
            string text = Normalize(raw);
            if (string.IsNullOrEmpty(text))
                continue;

            int score = 0;
            if (text.Contains("前往", StringComparison.Ordinal))
                score += 100;
            if (text.Contains("前", StringComparison.Ordinal))
                score += 40;
            if (text.Contains("往", StringComparison.Ordinal))
                score += 40;
            if (text.Contains("住", StringComparison.Ordinal))
                score += 10; // OCR 常把“往”识别成“住”

            if (score > bestScore)
            {
                bestScore = score;
                bestText = text;
                bestX = region.X + region.W / 2;
                bestY = region.Y + region.H / 2;
            }
        }

        if (bestScore >= 40)
            hitGoText = true;
        return (bestX, bestY);
    }

    /// <summary>
    /// 标准化 OCR 文本，去除空白字符
    /// </summary>
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
    private static readonly LogScope Log = new("Race:MovePlatform");
}

