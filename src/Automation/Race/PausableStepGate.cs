using SleepRunner.Utils;

namespace SleepRunner.Automation.Race;

/// <summary>
/// 可暂停的 step gate：常态直通，UI 触发 Pause 后会在每个 handler 之间阻塞
/// 设计要点：
/// - 不打断已进入 HandleAsync 的 handler，等当前步跑完再卡在下一次 dispatch 前
/// - 内部 ManualResetEventSlim 默认 set 状态（不阻塞），Pause 时 Reset，Resume 时 Set
/// - WaitForContinueAsync 在阻塞期间每 100ms 检查一次 cancellation，避免 Stop 卡住
/// </summary>
public sealed class PausableStepGate : IRaceStepGate, IDisposable
{
    private readonly ManualResetEventSlim _gate = new(initialState: true);
    private volatile bool _isPaused;
    private volatile bool _hasLoggedPause;

    public bool IsPaused => _isPaused;

    public void Pause()
    {
        if (_isPaused) return;
        _isPaused = true;
        _hasLoggedPause = false;
        _gate.Reset();
        Logger.Log("[Gate] Pause requested, will block before next handler dispatch");
    }

    public void Resume()
    {
        if (!_isPaused) return;
        _isPaused = false;
        _gate.Set();
        Logger.Log("[Gate] Resume requested, dispatch will continue");
    }

    public Task WaitForContinueAsync(string finishedStepSummary, CancellationToken cancellationToken)
    {
        if (!_isPaused)
            return Task.CompletedTask;

        if (!_hasLoggedPause)
        {
            _hasLoggedPause = true;
            Logger.Log($"[Gate] Paused at: {finishedStepSummary}");
        }

        // 在线程池上做阻塞等待，避免占用调用方线程；短轮询便于响应 Stop
        return Task.Run(() =>
        {
            while (_isPaused)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_gate.Wait(100, cancellationToken))
                    return;
            }
        }, cancellationToken);
    }

    public void Dispose() => _gate.Dispose();
}
