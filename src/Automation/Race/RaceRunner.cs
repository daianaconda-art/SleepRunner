using System.Diagnostics;
using OpenCvSharp;
using SleepRunner.Utils;
using SleepRunner.Vision;

namespace SleepRunner.Automation.Race;

/// <summary>
/// 跑马自动化主循环：轮询截屏 → 调度匹配的 Handler
/// </summary>
public class RaceRunner : IGameTask
{
    private readonly List<IRaceHandler> _handlers = [];
    private readonly IRaceStepGate? _stepGate;
    private readonly bool _exitAfterFirstHandledStep;
    private readonly bool _decisionOnly;
    private readonly bool _probeMoveOnly;
    private readonly bool _autoMode;

    // 自动模式：手动等待节流日志
    private string _lastManualHandler = "";
    private DateTime _lastManualLogUtc = DateTime.MinValue;

    public string Name => "跑马自动化";
    /// <param name="stepGate">非 null 时每完成一个 Handler 会等待用户确认再继续</param>
    /// <param name="exitAfterFirstHandledStep">为 true 时处理完第一个匹配的 Handler 后主循环结束（仅当前界面一步）</param>
    /// <param name="autoMode">自动连续模式：已知决策自动执行，未知跳过但保持探测</param>
    public RaceRunner(
        IRaceStepGate? stepGate = null,
        bool exitAfterFirstHandledStep = false,
        bool decisionOnly = false,
        bool probeMoveOnly = false,
        bool autoMode = false)
    {
        _stepGate = stepGate;
        _exitAfterFirstHandledStep = exitAfterFirstHandledStep;
        _decisionOnly = decisionOnly;
        _probeMoveOnly = probeMoveOnly;
        _autoMode = autoMode;
        Log.Log($"Race runner init: stepGate={stepGate != null}, once={exitAfterFirstHandledStep}, decisionOnly={decisionOnly}, probeMoveOnly={probeMoveOnly}, autoMode={autoMode}");

        RegisterHandler(new Handlers.SkipHandler());
        RegisterHandler(new Handlers.OverlayMenuHandler());
        RegisterHandler(new Handlers.MovePlatformHandler());
        RegisterHandler(new Handlers.EventHandler());
        RegisterHandler(new Handlers.CardSelectHandler());
        RegisterHandler(new Handlers.BattleHandler());
        RegisterHandler(new Handlers.AppraiseAcceptHandler());
        RegisterHandler(new Handlers.BattleDefeatHandler());
        RegisterHandler(new Handlers.BattleLeaveHandler());
        RegisterHandler(new Handlers.CommissionHandler());
        RegisterHandler(new Handlers.TradeAndAppraiseHandler());
        RegisterHandler(new Handlers.TradePurchaseHandler());
        RegisterHandler(new Handlers.RestDecisionHandler());
        RegisterHandler(new Handlers.JourneyEndHandler());
        RegisterHandler(new Handlers.MainMenuHandler());
        RegisterHandler(new Handlers.TrainingSelectHandler(stepGate));
    }

    public void RegisterHandler(IRaceHandler handler)
    {
        _handlers.Add(handler);
        _handlers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        Log.Log($"Handler registered: [{handler.Priority}] {handler.Name}");
    }

    public async Task RunAsync(GameContext ctx)
    {
        Log.Log("=== Race Automation: Start ===");
        Log.Log($"Registered {_handlers.Count} handlers");
        ActivityReporter.Set("启动中…");

        int idleCount = 0;
        int maxIdle = _autoMode ? 3600 : 600; // 自动模式30分钟，普通5分钟

        // 自适应轮询：连续未命中时阶梯式延长 wait，避免训练动画/转场期间无效探测耗 CPU
        int consecutiveMisses = 0;

        while (true)
        {
            ctx.CheckCancellation();

            // 每 tick 三段计时：capture/detect/exec，外加帧间 wait，便于定位"自动决策慢"的瓶颈
            var tickSw = Stopwatch.StartNew();
            long captureMs = 0;
            long detectMs = 0;
            long execMs = 0;
            long tickWaitMs = 0;
            string tickTag = "idle";

            var captureSw = Stopwatch.StartNew();
            using var shot = ctx.CaptureScreen();
            captureSw.Stop();
            captureMs = captureSw.ElapsedMilliseconds;

            if (shot == null || shot.Empty())
            {
                ActivityReporter.Set("等待截图…");
                tickWaitMs = await WaitMeasured(ctx, 500);
                Log.Log($"Tick: capture={captureMs}ms (empty) wait={tickWaitMs}ms total={tickSw.ElapsedMilliseconds}ms");
                continue;
            }

            // 仅在连续 miss 时才把 UI 状态切到"扫描"，避免单帧切换导致闪烁
            if (consecutiveMisses == 0)
                ActivityReporter.Set("识别画面…");

            // 单帧识别上下文：本 tick 内多个 Handler 共享 OCR 缓存，避免重复计算
            var frame = new FrameContext(shot);

            bool handled = false;
            bool exitAfterOnce = false;
            bool manualPause = false;
            var detectSw = Stopwatch.StartNew();
            foreach (var handler in _handlers)
            {
                if (handler.CanHandle(frame))
                {
                    string decision = handler.DescribeDecision(frame);
                    detectSw.Stop();
                    detectMs = detectSw.ElapsedMilliseconds;
                    Log.Log($">>> Handler matched: {handler.Name}");
                    Log.Log($"Decision preview: {decision}");
                    ActivityReporter.Set($"决策中：{handler.Name}");

                    // 自动连续模式：需人工介入时跳过执行，保持探测循环
                    if (_autoMode && IsManualDecision(decision))
                    {
                        LogManualReminder(handler.Name, decision);
                        ActivityReporter.Set($"需人工：{handler.Name}");
                        manualPause = true;
                        tickTag = $"manual:{handler.Name}";
                        break;
                    }

                    // 自动模式下从手动等待恢复
                    if (_autoMode) ClearManualState();

                    if (_stepGate != null)
                    {
                        string summary = $"{handler.Name} -> {decision}";
                        Log.Log("Step gate: waiting for user confirmation before execute...");
                        ActivityReporter.Set("等待确认");
                        await _stepGate.WaitForContinueAsync(summary, ctx.CancellationToken);
                        Log.Log("Step gate: execute confirmed.");
                    }

                    if (_decisionOnly)
                    {
                        Log.Log("Decision-only mode: preview generated, skip execution.");
                        handled = true;
                        idleCount = 0;
                        exitAfterOnce = true;
                        tickTag = $"decision:{handler.Name}";
                        break;
                    }

                    var execSw = Stopwatch.StartNew();
                    if (_probeMoveOnly)
                    {
                        Log.Log("Probe-move mode: run probe only, skip click execution.");
                        ActivityReporter.Set($"探测：{handler.Name}");
                        await handler.ProbeAsync(ctx);
                    }
                    else
                    {
                        ActivityReporter.Set($"执行：{handler.Name}");
                        await handler.HandleAsync(ctx);
                    }
                    execSw.Stop();
                    execMs = execSw.ElapsedMilliseconds;
                    handled = true;
                    idleCount = 0;
                    consecutiveMisses = 0;
                    tickTag = $"exec:{handler.Name}";

                    if (_exitAfterFirstHandledStep)
                    {
                        Log.Log("Exit after first handled step (once mode).");
                        exitAfterOnce = true;
                    }

                    break;
                }
            }
            // 没匹配到任何 handler 时 detectSw 仍在跑，停掉收口
            if (detectSw.IsRunning)
            {
                detectSw.Stop();
                detectMs = detectSw.ElapsedMilliseconds;
            }

            if (exitAfterOnce)
            {
                Log.Log($"Tick: capture={captureMs}ms detect={detectMs}ms exec={execMs}ms ({tickTag}) total={tickSw.ElapsedMilliseconds}ms");
                break;
            }

            // 自动模式人工等待：不执行、不累积idle、稍长间隔后继续探测
            if (manualPause)
            {
                idleCount = 0;
                consecutiveMisses = 0;
                tickWaitMs = await WaitMeasured(ctx, 2000);
                Log.Log($"Tick: capture={captureMs}ms detect={detectMs}ms wait={tickWaitMs}ms ({tickTag}) total={tickSw.ElapsedMilliseconds}ms");
                continue;
            }

            if (!handled)
            {
                // 无 Handler 匹配时清除手动状态（画面已被人工改变）
                if (_autoMode) ClearManualState();

                if (_decisionOnly)
                {
                    Log.Log("Decision-only mode: no handler matched on current frame.");
                    Log.Log($"Tick: capture={captureMs}ms detect={detectMs}ms ({tickTag}) total={tickSw.ElapsedMilliseconds}ms");
                    break;
                }

                idleCount++;
                if (idleCount >= maxIdle)
                {
                    Log.Log(_autoMode
                        ? "Auto mode: 30 min idle, stopping."
                        : "Max idle reached (5 min), stopping.");
                    break;
                }

                consecutiveMisses++;
                // 连续未命中 ≥2 次再切到"等待画面变化"，让 UI 知道处于转场/动画期
                if (consecutiveMisses >= 2)
                    ActivityReporter.Set("等待画面变化…");
            }

            // 单帧识别耗时若超阈值，自动打印 Top-N 慢调用，便于定位拖慢决策的具体 OCR
            frame.DumpIfSlow(thresholdMs: 800, moduleTag: "Race");

            // 自适应 wait：连续 miss 时阶梯延长（500/800/1200/1800/1800ms），首次命中即重置
            // 训练动画/转场期通常 4-7s，长 wait 能吃掉 2-3 次无效 handler 链探测
            int baseWaitMs = consecutiveMisses switch
            {
                0 => 500,
                1 => 500,
                2 => 800,
                3 => 1200,
                _ => 1800,
            };
            tickWaitMs = await WaitMeasured(ctx, baseWaitMs);

            Log.Log($"Tick: capture={captureMs}ms detect={detectMs}ms exec={execMs}ms wait={tickWaitMs}ms ({tickTag}) total={tickSw.ElapsedMilliseconds}ms");
        }

        Log.Log("=== Race Automation: Done ===");
        ActivityReporter.Set("已结束");
    }

    /// <summary>
    /// 判断决策描述是否需要人工介入
    /// </summary>
    private static bool IsManualDecision(string decision)
    {
        return decision.Contains("wait manual", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 节流输出人工等待提醒（同 Handler 10秒内不重复）
    /// </summary>
    private void LogManualReminder(string handlerName, string decision)
    {
        bool isNew = handlerName != _lastManualHandler;
        bool expired = (DateTime.UtcNow - _lastManualLogUtc).TotalSeconds >= 10;

        if (isNew || expired)
        {
            if (isNew)
            {
                Log.Log("AUTO: *** MANUAL INTERVENTION NEEDED ***");
                Log.Log($"AUTO: {handlerName} -> {decision}");
                Log.Log("AUTO: Please resolve this screen manually. Probing continues, automation will resume once screen changes.");
            }
            else
            {
                Log.Log($"AUTO: Still waiting for manual: {handlerName}");
            }
            _lastManualHandler = handlerName;
            _lastManualLogUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 包一层 Stopwatch 调用 ctx.Wait，返回实际等待毫秒（受 RaceConfig.WaitMultiplier 缩放后的真实耗时）
    /// </summary>
    private static async Task<long> WaitMeasured(GameContext ctx, int baseMs)
    {
        var sw = Stopwatch.StartNew();
        await ctx.Wait(baseMs);
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    /// <summary>
    /// 清除手动等待状态（画面已变化或已自动处理）
    /// </summary>
    private void ClearManualState()
    {
        if (!string.IsNullOrEmpty(_lastManualHandler))
        {
            Log.Log("AUTO: Manual situation resolved, automation resumed.");
            _lastManualHandler = "";
        }
    }
    private static readonly LogScope Log = new("Race");
}
