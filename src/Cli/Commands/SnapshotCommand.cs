using SleepRunner.Supervision;
using SleepRunner.Utils;

namespace SleepRunner.Cli.Commands;

/// <summary>
/// --snapshot [outputPath]：手工抓拍游戏当前画面（不写持久日志）
/// </summary>
internal sealed class SnapshotCommand : ICliCommand, IEphemeralCliCommand
{
    public string Name => "--snapshot";

    public Task<int> ExecuteAsync(string[] args)
    {
        CliBootstrap.EnsureUtf8Console();
        Console.WriteLine("=== SleepRunner Snapshot Mode ===");
        Console.WriteLine($"Base dir: {PathHelper.BaseDir}");

        string? outputPath = args.Length >= 2 ? args[1] : null;
        string savedPath = SnapshotService.CaptureGameSnapshot(outputPath, filePrefix: "manual");
        Console.WriteLine($"Snapshot saved: {savedPath}");
        return Task.FromResult(0);
    }
}
