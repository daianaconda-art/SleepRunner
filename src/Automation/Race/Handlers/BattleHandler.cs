using OpenCvSharp;
using SleepRunner.Automation.Race.Handlers.Battle;
using SleepRunner.Input;
using SleepRunner.Utils;
using SleepRunner.Vision;

namespace SleepRunner.Automation.Race.Handlers;

/// <summary>
/// 战斗分支入口：底部出现 BURST 字样即判定进入战斗界面。
///
/// 重构后职责（编排层）：
/// - CanHandle / DescribeDecision：调用 Battle/* 子模块做指纹与状态识别
/// - HandleAsync / ProbeAsync：决定 ON/OFF 后做点击或等待
/// - BURST 文本识别 + AUTO 颜色判断 全部委托给 Battle/ 子模块
/// </summary>
public class BattleHandler : IRaceHandler
{
    public string Name => "战斗分支";
    public int Priority => 12;

    // 战斗内仍以游戏自动战斗为主，脚本只负责确保右上角 AUTO 处于开启状态
    private const bool MaintainAutoToggleOnly = true;

    private readonly BattleAutoToggle _autoToggle = new();

    public bool CanHandle(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        string burstText = BattleScreenChecks.ReadBurstText(screenshot);
        bool hit = BattleScreenChecks.IsBurstText(burstText);
        if (hit)
            Log.Log($"Battle marker hit: '{burstText}'");
        else if (!string.IsNullOrEmpty(burstText))
            Log.Log($"Battle marker miss: '{burstText}'");
        return hit;
    }

    public string DescribeDecision(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        string burstText = BattleScreenChecks.ReadBurstText(screenshot);
        var state = _autoToggle.DetectAutoState(screenshot, out double satMean, out double valMean, out var autoCenter, out double autoConf);
        var clickPoint = BattleAutoToggle.ResolveClickPoint(screenshot, autoCenter);
        return state switch
        {
            AutoState.OffGray => $"Battle: BURST='{burstText}', auto=OFF(gray sat={satMean:F1},val={valMean:F1},conf={autoConf:F3}) -> click auto ({clickPoint.X},{clickPoint.Y})",
            AutoState.OnBright => $"Battle: BURST='{burstText}', auto=ON(bright sat={satMean:F1},val={valMean:F1}) -> wait",
            _ => $"Battle: BURST='{burstText}', auto=UNKNOWN(sat={satMean:F1},val={valMean:F1}) -> wait"
        };
    }

    public async Task HandleAsync(GameContext ctx)
    {
        using var shot = ctx.CaptureScreen();
        if (shot == null || shot.Empty())
        {
            Log.Log("Battle branch: capture empty, wait.");
            await ctx.Wait(600);
            return;
        }

        var state = _autoToggle.DetectAutoState(shot, out double satMean, out double valMean, out var autoCenter, out double autoConf);
        var clickPoint = BattleAutoToggle.ResolveClickPoint(shot, autoCenter);
        if (state == AutoState.OffGray)
        {
            Log.Log($"Battle branch: auto is OFF(gray sat={satMean:F1}, val={valMean:F1}, conf={autoConf:F3}), clickPoint=({clickPoint.X},{clickPoint.Y}), reason='{clickPoint.Reason}'");
            await MouseSimulator.MoveToClient(ctx.WindowHandle, clickPoint.X, clickPoint.Y);
            await ctx.Wait(320);
            await MouseSimulator.ClickAtClient(ctx.WindowHandle, clickPoint.X, clickPoint.Y);
            await ctx.Wait(700);
            return;
        }

        if (state == AutoState.OnBright)
        {
            if (MaintainAutoToggleOnly)
                Log.Log($"Battle branch: auto is ON(bright sat={satMean:F1}, val={valMean:F1}), keep waiting.");
            await ctx.Wait(1000);
            return;
        }

        Log.Log($"Battle branch: auto state unknown(sat={satMean:F1}, val={valMean:F1}), safe wait.");
        await ctx.Wait(800);
    }

    /// <summary>
    /// 探测模式：识别自动按钮位置并移动鼠标过去，不点击
    /// </summary>
    public async Task ProbeAsync(GameContext ctx)
    {
        using var shot = ctx.CaptureScreen();
        if (shot == null || shot.Empty())
        {
            Log.Log("Battle probe: capture empty, skip move.");
            return;
        }

        var state = _autoToggle.DetectAutoState(shot, out double satMean, out double valMean, out var autoCenter, out double autoConf);
        var clickPoint = BattleAutoToggle.ResolveClickPoint(shot, autoCenter);
        if (state == AutoState.Unknown)
        {
            Log.Log($"Battle probe: no stable auto center (state={state}, sat={satMean:F1}, val={valMean:F1}, conf={autoConf:F3}), skip move.");
            return;
        }

        Log.Log($"Battle probe: move cursor to auto point=({clickPoint.X},{clickPoint.Y}), state={state}, conf={autoConf:F3}, reason='{clickPoint.Reason}'");
        await MouseSimulator.MoveToClient(ctx.WindowHandle, clickPoint.X, clickPoint.Y);
        await ctx.Wait(300);
    }
    private static readonly LogScope Log = new("Race:Battle");
}
