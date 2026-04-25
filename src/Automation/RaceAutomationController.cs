using SleepRunner.Automation.Race;
using SleepRunner.Capture;
using SleepRunner.Forms;
using SleepRunner.Automation.Race.Policy;
using SleepRunner.Automation.Race.Policy.Training;
using SleepRunner.Utils;

namespace SleepRunner.Automation;

/// <summary>
/// 跑马自动化生命周期控制器：包装 RaceRunner + GameContext + 取消令牌
/// 给 UI 提供 Start / Pause / Resume / Stop 四个操作和状态变化事件
/// 同一时刻只允许一个 race session 运行
/// </summary>
public sealed class RaceAutomationController : IRaceController
{
    private readonly object _lifecycleLock = new();
    private readonly PausableStepGate _gate = new();

    private CancellationTokenSource? _cts;
    private BitBltCapture? _capture;
    private GameContext? _ctx;
    private Task? _runTask;
    private RaceState _state = RaceState.Idle;

    /// <summary>当前状态。状态变化由 StateChanged 事件通知</summary>
    public RaceState State
    {
        get { lock (_lifecycleLock) return _state; }
    }

    /// <summary>状态变化事件（在后台线程触发，UI 订阅时需 BeginInvoke 回主线程）</summary>
    public event Action<RaceState>? StateChanged;

    /// <summary>
    /// 当前活动描述变化（来自 ActivityReporter.Changed 的转发）
    /// 让外部 UI 不必直接耦合静态 ActivityReporter
    /// </summary>
    public event Action<string>? ActivityChanged;

    public RaceAutomationController()
    {
        // 把全局活动广播桥接到本控制器事件
        ActivityReporter.Changed += OnActivityChanged;
    }

    private void OnActivityChanged(string text)
    {
        try { ActivityChanged?.Invoke(text); }
        catch (Exception ex) { Logger.Log($"[Controller] ActivityChanged handler threw: {ex.Message}"); }
    }

    /// <summary>
    /// 启动跑马自动化（autoMode=true，等价于 CLI --race-auto）
    /// 已在运行时调用直接返回
    /// </summary>
    public void Start()
    {
        lock (_lifecycleLock)
        {
            if (_state == RaceState.Running || _state == RaceState.Paused || _state == RaceState.Stopping)
            {
                Logger.Log($"[Controller] Start ignored: current state={_state}");
                return;
            }

            var hWnd = WindowHelper.FindTargetWindow();
            if (hWnd == IntPtr.Zero)
            {
                Logger.Log("[Controller] Start failed: target window not found");
                TransitionTo(RaceState.Idle);
                return;
            }

            _capture = new BitBltCapture();
            _capture.Start(hWnd);

            Logger.StartSession("race_run");
            _cts = new CancellationTokenSource();
            _ctx = new GameContext(hWnd, _capture, _cts.Token);

            var task = new RaceRunner(
                stepGate: _gate,
                exitAfterFirstHandledStep: false,
                decisionOnly: false,
                probeMoveOnly: false,
                autoMode: true);

            Logger.Log("[Controller] Start race session");
            Logger.Log($"[Controller] Log file: {Logger.CurrentSessionPath ?? "(none)"}");
            Logger.Log(
                $"[Controller] Profiles: events={RaceProfileManager.CurrentEventsProfile}, " +
                $"cards={RaceProfileManager.CurrentCardsProfile}, trade={RaceProfileManager.CurrentTradeProfile}, " +
                $"training={TrainingRuleProfileManager.CurrentProfile}");
            TransitionTo(RaceState.Running);

            _runTask = Task.Run(async () =>
            {
                try
                {
                    await task.RunAsync(_ctx);
                    Logger.Log("[Controller] Race task finished naturally");
                }
                catch (OperationCanceledException)
                {
                    Logger.Log("[Controller] Race task cancelled");
                }
                catch (RaceTaskCompletedException ex)
                {
                    Logger.Log($"[Controller] Race task completed: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Logger.Log($"[Controller] Race task crashed: {ex.GetType().Name}: {ex.Message}");
                }
                finally
                {
                    DisposeRunResources();
                    ActivityReporter.Clear();
                    TransitionTo(RaceState.Stopped);
                    Logger.EndSession();
                }
            });
        }
    }

    /// <summary>暂停：当前 handler 跑完后停在下一次 dispatch 前</summary>
    public void Pause()
    {
        lock (_lifecycleLock)
        {
            if (_state != RaceState.Running)
            {
                Logger.Log($"[Controller] Pause ignored: current state={_state}");
                return;
            }
            _gate.Pause();
            TransitionTo(RaceState.Paused);
        }
    }

    /// <summary>从暂停恢复</summary>
    public void Resume()
    {
        lock (_lifecycleLock)
        {
            if (_state != RaceState.Paused)
            {
                Logger.Log($"[Controller] Resume ignored: current state={_state}");
                return;
            }
            _gate.Resume();
            TransitionTo(RaceState.Running);
        }
    }

    /// <summary>停止：取消令牌触发，等待 race task 退出</summary>
    public async Task StopAsync()
    {
        Task? toAwait = null;
        lock (_lifecycleLock)
        {
            if (_state == RaceState.Idle || _state == RaceState.Stopped)
                return;
            if (_state == RaceState.Stopping)
            {
                toAwait = _runTask;
            }
            else
            {
                Logger.Log("[Controller] Stop requested");
                _cts?.Cancel();
                // 暂停态先唤醒 gate，让 WaitForContinueAsync 抛出取消
                if (_state == RaceState.Paused)
                    _gate.Resume();
                TransitionTo(RaceState.Stopping);
                toAwait = _runTask;
            }
        }

        if (toAwait != null)
        {
            try { await toAwait.ConfigureAwait(false); }
            catch { /* 已在 task 内 catch 过 */ }
        }
    }

    private void DisposeRunResources()
    {
        lock (_lifecycleLock)
        {
            try { _ctx?.Dispose(); } catch { }
            try { _capture?.Dispose(); } catch { }
            try { _cts?.Dispose(); } catch { }
            _ctx = null;
            _capture = null;
            _cts = null;
            _runTask = null;
        }
    }

    private void TransitionTo(RaceState next)
    {
        // 调用方已持有 _lifecycleLock
        if (_state == next)
            return;
        _state = next;
        try
        {
            StateChanged?.Invoke(next);
        }
        catch (Exception ex)
        {
            Logger.Log($"[Controller] StateChanged handler threw: {ex.Message}");
        }
    }

    public void Dispose()
    {
        try { StopAsync().GetAwaiter().GetResult(); } catch { }
        ActivityReporter.Changed -= OnActivityChanged;
        _gate.Dispose();
    }
}

public enum RaceState
{
    Idle,
    Running,
    Paused,
    Stopping,
    Stopped,
}
