using SleepRunner.Capture;
using SleepRunner.Forms;
using SleepRunner.Utils;

namespace SleepRunner.Automation.BuiltInRace;

public sealed class BuiltInRaceController : IRaceController
{
    private readonly object _lifecycleLock = new();

    private CancellationTokenSource? _cts;
    private BitBltCapture? _capture;
    private GameContext? _ctx;
    private Task? _runTask;
    private RaceState _state = RaceState.Idle;

    public RaceState State
    {
        get { lock (_lifecycleLock) return _state; }
    }

    public event Action<RaceState>? StateChanged;
    public event Action<string>? ActivityChanged;

    public void Start()
    {
        lock (_lifecycleLock)
        {
            if (_state is RaceState.Running or RaceState.Paused or RaceState.Stopping)
            {
                Logger.Log($"[BuiltInRaceController] Start ignored: current state={_state}");
                return;
            }

            IntPtr hWnd = WindowHelper.FindTargetWindow();
            if (hWnd == IntPtr.Zero)
            {
                Logger.Log("[BuiltInRaceController] Start failed: target window not found");
                TransitionTo(RaceState.Idle);
                return;
            }

            _capture = new BitBltCapture();
            _capture.Start(hWnd);
            _cts = new CancellationTokenSource();
            _ctx = new GameContext(hWnd, _capture, _cts.Token);

            var runner = new BuiltInRaceRunner(NotifyActivity);

            Logger.StartSession("built_in_race");
            Logger.Log("[BuiltInRaceController] Start built-in race session");
            Logger.Log($"[BuiltInRaceController] Log file: {Logger.CurrentSessionPath ?? "(none)"}");
            TransitionTo(RaceState.Running);

            _runTask = Task.Run(async () =>
            {
                try
                {
                    await runner.RunAsync(_ctx);
                    Logger.Log("[BuiltInRaceController] Built-in race task finished naturally");
                }
                catch (OperationCanceledException)
                {
                    Logger.Log("[BuiltInRaceController] Built-in race task cancelled");
                }
                catch (Exception ex)
                {
                    Logger.Log($"[BuiltInRaceController] Built-in race task crashed: {ex.GetType().Name}: {ex.Message}");
                }
                finally
                {
                    DisposeRunResources();
                    NotifyActivity(string.Empty);
                    TransitionTo(RaceState.Stopped);
                    Logger.EndSession();
                }
            });
        }
    }

    public void Pause()
    {
    }

    public void Resume()
    {
    }

    public async Task StopAsync()
    {
        Task? toAwait = null;
        lock (_lifecycleLock)
        {
            if (_state is RaceState.Idle or RaceState.Stopped)
                return;

            if (_state == RaceState.Stopping)
            {
                toAwait = _runTask;
            }
            else
            {
                Logger.Log("[BuiltInRaceController] Stop requested");
                _cts?.Cancel();
                TransitionTo(RaceState.Stopping);
                toAwait = _runTask;
            }
        }

        if (toAwait != null)
        {
            try { await toAwait.ConfigureAwait(false); }
            catch { }
        }
    }

    private void NotifyActivity(string text)
    {
        try { ActivityChanged?.Invoke(text); }
        catch (Exception ex) { Logger.Log($"[BuiltInRaceController] ActivityChanged handler threw: {ex.Message}"); }
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
        if (_state == next)
            return;

        _state = next;
        try
        {
            StateChanged?.Invoke(next);
        }
        catch (Exception ex)
        {
            Logger.Log($"[BuiltInRaceController] StateChanged handler threw: {ex.Message}");
        }
    }

    public void Dispose()
    {
        try { StopAsync().GetAwaiter().GetResult(); } catch { }
    }
}
