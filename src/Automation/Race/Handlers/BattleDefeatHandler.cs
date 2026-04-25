using OpenCvSharp;
using SleepRunner.Input;
using SleepRunner.Recognition;
using SleepRunner.Utils;
using SleepRunner.Vision;

namespace SleepRunner.Automation.Race.Handlers;

/// <summary>
/// 战斗失败重试分支：识别右上角 DEFEAT 大字 → 点击右下"重新挑战" → 弹窗"确认"
///
/// 优先级 13，比 BattleLeaveHandler(14) 高一级。
/// 否则 BattleLeaveHandler 的 IsVictoryResultScreen 因为含"达成度"会把 DEFEAT 页当胜利结算页直接点"离开"。
///
/// 兜底：若重新挑战次数耗尽（OCR 找不到"重新挑战"按钮文字），CanHandle 让出，
///       后续由 BattleLeaveHandler 接管点离开按钮。
/// </summary>
public class BattleDefeatHandler : IRaceHandler
{
    public string Name => "战斗失败重试";
    public int Priority => 13;

    // 右上 DEFEAT 大字检测区
    private const double DefeatTextX = 0.66;
    private const double DefeatTextY = 0.08;
    private const double DefeatTextW = 0.34;
    private const double DefeatTextH = 0.40;

    // 右下"重新挑战 X N"按钮文字区（白色按钮，DEFEAT 页底部偏左）
    private const double RetryBtnTextX = 0.60;
    private const double RetryBtnTextY = 0.88;
    private const double RetryBtnTextW = 0.22;
    private const double RetryBtnTextH = 0.10;

    // 重新挑战按钮中心点
    private const double RetryBtnClickX = 0.74;
    private const double RetryBtnClickY = 0.93;

    // "重新挑战通知"弹窗标题/正文检测区
    private const double DialogTitleX = 0.20;
    private const double DialogTitleY = 0.15;
    private const double DialogTitleW = 0.60;
    private const double DialogTitleH = 0.30;

    // 弹窗"确认"按钮中心点（蓝色，右侧）
    private const double DialogConfirmX = 0.58;
    private const double DialogConfirmY = 0.68;

    public bool CanHandle(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        // 优先检测弹窗：用户可能手动点过重新挑战，弹窗已开
        if (IsRetryDialog(screenshot))
        {
            Log.Log("Retry dialog detected.");
            return true;
        }

        if (!IsDefeatScreen(screenshot))
            return false;

        // DEFEAT 页：必须能看到"重新挑战"按钮文字才接管，否则让 BattleLeaveHandler 点离开
        if (!HasRetryButtonText(screenshot, out string retryText))
        {
            Log.Log($"DEFEAT detected but retry button unavailable (text='{retryText}'), yield to BattleLeave.");
            return false;
        }

        Log.Log($"DEFEAT screen detected, retry available (text='{retryText}').");
        return true;
    }

    public string DescribeDecision(FrameContext frame)
        => "BattleDefeat: click 重新挑战 then 确认";

    public async Task HandleAsync(GameContext ctx)
    {
        // 弹窗已开 → 直接点确认
        using (var shot = ctx.CaptureScreen())
        {
            if (shot != null && !shot.Empty() && IsRetryDialog(shot))
            {
                Log.Log("Retry dialog already open, click 确认.");
                await ctx.ClickAtPercent(DialogConfirmX, DialogConfirmY);
                await ctx.Wait(1800);
                return;
            }
        }

        Log.Log("Click 重新挑战 button.");
        await ctx.ClickAtPercent(RetryBtnClickX, RetryBtnClickY);
        await ctx.Wait(800);

        // 等弹窗弹出，最多探测 4 次（约 2 秒）
        bool dialogOpened = false;
        for (int attempt = 1; attempt <= 4; attempt++)
        {
            using var shot = ctx.CaptureScreen();
            if (shot != null && !shot.Empty() && IsRetryDialog(shot))
            {
                Log.Log($"Retry dialog opened after attempt={attempt}, click 确认.");
                await ctx.ClickAtPercent(DialogConfirmX, DialogConfirmY);
                await ctx.Wait(1800);
                dialogOpened = true;
                break;
            }
            await ctx.Wait(500);
        }

        if (!dialogOpened)
        {
            Log.Log("Retry dialog did not open in time, will retry next loop.");
        }
    }

    public async Task ProbeAsync(GameContext ctx)
    {
        using var shot = ctx.CaptureScreen();
        if (shot == null || shot.Empty())
        {
            Log.Log("Defeat probe: capture empty, skip move.");
            return;
        }

        double targetX, targetY;
        string label;
        if (IsRetryDialog(shot))
        {
            targetX = DialogConfirmX;
            targetY = DialogConfirmY;
            label = "弹窗确认";
        }
        else
        {
            targetX = RetryBtnClickX;
            targetY = RetryBtnClickY;
            label = "重新挑战";
        }

        int x = (int)(shot.Width * targetX);
        int y = (int)(shot.Height * targetY);
        Log.Log($"Defeat probe: move cursor to {label} ({x},{y}).");
        await MouseSimulator.MoveToClient(ctx.WindowHandle, x, y);
        await ctx.Wait(300);
    }

    private static bool IsDefeatScreen(Mat screenshot)
    {
        string raw = OcrHelper.RecognizeRegion(screenshot, DefeatTextX, DefeatTextY, DefeatTextW, DefeatTextH)
            .GetAwaiter()
            .GetResult();
        string text = Normalize(raw).ToUpperInvariant();
        if (string.IsNullOrEmpty(text))
            return false;

        // 完整 DEFEAT 命中
        if (text.Contains("DEFEAT", StringComparison.Ordinal))
            return true;

        // OCR 经常把 DEFEAT 误识为 OEFEAT/DEFFAT/DEPEAT 等：含"EFEAT"也接受
        if (text.Contains("EFEAT", StringComparison.Ordinal))
            return true;

        return false;
    }

    private static bool HasRetryButtonText(Mat screenshot, out string text)
    {
        string raw = OcrHelper.RecognizeRegion(screenshot, RetryBtnTextX, RetryBtnTextY, RetryBtnTextW, RetryBtnTextH)
            .GetAwaiter()
            .GetResult();
        text = Normalize(raw);
        if (string.IsNullOrEmpty(text))
            return false;

        // "重新挑战" 或部分残缺命中
        if (text.Contains("重新挑战", StringComparison.Ordinal))
            return true;
        if (text.Contains("重新", StringComparison.Ordinal) && text.Contains("挑战", StringComparison.Ordinal))
            return true;
        return false;
    }

    private static bool IsRetryDialog(Mat screenshot)
    {
        string raw = OcrHelper.RecognizeRegion(screenshot, DialogTitleX, DialogTitleY, DialogTitleW, DialogTitleH)
            .GetAwaiter()
            .GetResult();
        string text = Normalize(raw);
        if (string.IsNullOrEmpty(text))
            return false;

        // 弹窗强指纹：标题"重新挑战通知" 或 正文"是否要重新挑战" / "再次尝试战斗"
        if (text.Contains("重新挑战通知", StringComparison.Ordinal))
            return true;
        if (text.Contains("是否要重新挑战", StringComparison.Ordinal))
            return true;
        if (text.Contains("再次尝试战斗", StringComparison.Ordinal))
            return true;
        // 弱兜底：同时含"重新挑战"和"次"（弹窗里有"重新挑战可再N次"）
        if (text.Contains("重新挑战", StringComparison.Ordinal) &&
            (text.Contains("再", StringComparison.Ordinal) && text.Contains("次", StringComparison.Ordinal)))
            return true;
        return false;
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
    private static readonly LogScope Log = new("Race:BattleDefeat");
}
