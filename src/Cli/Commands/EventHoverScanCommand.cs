using SleepRunner.Automation;
using SleepRunner.Automation.Race;
using SleepRunner.Capture;
using SleepRunner.Utils;

namespace SleepRunner.Cli.Commands;

/// <summary>
/// --event-hover-scan [survival]：事件页选项 hover 扫描，配合 EventHandler 调试
/// </summary>
internal sealed class EventHoverScanCommand : ICliCommand
{
    public string Name => "--event-hover-scan";

    public async Task<int> ExecuteAsync(string[] args)
    {
        CliBootstrap.EnsureUtf8Console();
        var direction = CliBootstrap.ParseRaceBuildDirection(args, startIndex: 1);

        Console.WriteLine("=== Event Hover Scan ===");
        Console.WriteLine($"Base dir: {PathHelper.BaseDir}");
        Console.WriteLine($"Build direction: {direction}");

        var hWnd = CliBootstrap.FindGameOrLogError();
        if (hWnd == IntPtr.Zero)
            return 1;

        using var capture = new BitBltCapture();
        capture.Start(hWnd);
        using var cts = new CancellationTokenSource();
        using var ctx = new GameContext(hWnd, capture, cts.Token);
        var handler = new Automation.Race.Handlers.EventHandler();
        await handler.DebugHoverScanAsync(ctx);
        return 0;
    }
}
