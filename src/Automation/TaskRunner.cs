using SleepRunner.Capture;
using SleepRunner.Utils;

namespace SleepRunner.Automation;

/// <summary>
/// 任务运行状态
/// </summary>
public enum TaskStatus
{
    Idle,
    Running,
    Completed,
    Cancelled,
    Error
}

/// <summary>
/// 任务执行器，管理任务的生命周期（启动/取消/状态通知）
/// </summary>
public class TaskRunner
{
    private CancellationTokenSource? _cts;
    private Task? _runningTask;

    /// <summary>
    /// 任务状态变更事件，UI 可订阅以更新界面
    /// </summary>
    public event Action<TaskStatus, string>? OnStatusChanged;

    /// <summary>
    /// 当前是否有任务在运行
    /// </summary>
    public bool IsRunning => _runningTask != null && !_runningTask.IsCompleted;

    /// <summary>
    /// 启动一个任务，若已有任务在跑则拒绝
    /// </summary>
    public void Start(IGameTask task, IntPtr hWnd, BitBltCapture capture)
    {
        if (IsRunning)
        {
            Log.Log("Cannot start: a task is already running");
            return;
        }

        Logger.StartSession($"gui_{task.Name}");
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        NotifyStatus(TaskStatus.Running, task.Name);
        Log.Log($"Task '{task.Name}' starting...");

        _runningTask = Task.Run(async () =>
        {
            using var ctx = new GameContext(hWnd, capture, token);
            try
            {
                await task.RunAsync(ctx);
                Log.Log($"Task '{task.Name}' completed successfully");
                NotifyStatus(TaskStatus.Completed, task.Name);
            }
            catch (OperationCanceledException)
            {
                Log.Log($"Task '{task.Name}' was cancelled by user");
                NotifyStatus(TaskStatus.Cancelled, task.Name);
            }
            catch (Exception ex)
            {
                Log.Log($"Task '{task.Name}' failed: {ex.Message}");
                NotifyStatus(TaskStatus.Error, $"{task.Name}: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// 取消当前运行的任务
    /// </summary>
    public void Cancel()
    {
        if (_cts == null || !IsRunning)
        {
            Log.Log("No running task to cancel");
            return;
        }

        Log.Log("Cancelling task...");
        _cts.Cancel();
    }

    private void NotifyStatus(TaskStatus status, string info)
    {
        OnStatusChanged?.Invoke(status, info);
    }
    private static readonly LogScope Log = new("Runner");
}
