namespace SleepRunner.Automation;

/// <summary>
/// 自动化任务接口，所有可执行任务都实现此接口
/// </summary>
public interface IGameTask
{
    /// <summary>
    /// 任务显示名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 执行任务，由 TaskRunner 调用
    /// </summary>
    Task RunAsync(GameContext ctx);
}
