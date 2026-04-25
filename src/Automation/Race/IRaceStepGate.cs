namespace SleepRunner.Automation.Race;

/// <summary>
/// 跑马单步模式：每处理完一个 Handler 后挂起，直到用户确认再继续
/// </summary>
public interface IRaceStepGate
{
    /// <summary>
    /// 当前步已执行完毕，在返回前阻塞直至用户允许进入下一轮（或取消令牌触发）
    /// </summary>
    Task WaitForContinueAsync(string finishedStepSummary, CancellationToken cancellationToken);
}
