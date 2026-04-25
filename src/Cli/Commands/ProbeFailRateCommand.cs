using SleepRunner.Automation;
using SleepRunner.Automation.Race.Handlers;
using SleepRunner.Capture;
using SleepRunner.Utils;

namespace SleepRunner.Cli.Commands;

/// <summary>
/// --probe-fail-rate [xPct yPct]：仅探测失败率；可先点击指定行
/// </summary>
internal sealed class ProbeFailRateCommand : ICliCommand
{
    public string Name => "--probe-fail-rate";

    public async Task<int> ExecuteAsync(string[] args)
    {
        CliBootstrap.EnsureUtf8Console();
        var settings = UserSettings.Load();
        settings.ApplyToRaceConfig();
        Console.WriteLine("=== Probe Fail Rate (no train/return actions) ===");

        var hWnd = CliBootstrap.FindGameOrLogError();
        if (hWnd == IntPtr.Zero)
            return 1;

        using var capture = new BitBltCapture();
        capture.Start(hWnd);
        using var cts = new CancellationTokenSource();
        using var ctx = new GameContext(hWnd, capture, cts.Token);

        if (args.Length >= 3 &&
            double.TryParse(args[1], out double xp) &&
            double.TryParse(args[2], out double yp))
        {
            Console.WriteLine($"Pre-click training row at ({xp:F2},{yp:F2})...");
            await ctx.ClickAtPercent(xp, yp);
            await ctx.Wait(650);
        }

        var handler = new TrainingSelectHandler();
        int failRate = await handler.ProbeFailRateNowAsync(ctx);
        Console.WriteLine($"Detected fail rate: {failRate}%");
        return 0;
    }
}
