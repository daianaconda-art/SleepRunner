using SleepRunner.Automation.Race;

namespace SleepRunner.Cli;

/// <summary>
/// CLI 命令统一接口
/// </summary>
public interface ICliCommand
{
    /// <summary>命令名（含前缀，如 "--test"）</summary>
    string Name { get; }

    /// <summary>执行命令；返回退出码（0 = 成功）</summary>
    Task<int> ExecuteAsync(string[] args);
}

/// <summary>
/// 部分 CLI 命令需要切换日志策略（snapshot 等临时命令不写持久日志）
/// </summary>
public interface IEphemeralCliCommand
{
}
