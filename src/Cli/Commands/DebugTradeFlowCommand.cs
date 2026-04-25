using SleepRunner.Automation;
using SleepRunner.Automation.Race.Handlers.Trade;
using SleepRunner.Automation.Race.Policy;
using SleepRunner.Capture;
using SleepRunner.Utils;

namespace SleepRunner.Cli.Commands;

/// <summary>
/// --debug-trade-flow：交易页完整流程验证（不购买），用于验证顺序槽位状态机
/// </summary>
internal sealed class DebugTradeFlowCommand : ICliCommand
{
    public string Name => "--debug-trade-flow";

    public async Task<int> ExecuteAsync(string[] args)
    {
        CliBootstrap.EnsureUtf8Console();
        var settings = UserSettings.Load();
        settings.ApplyToRaceConfig();

        Console.WriteLine("=== Debug Trade Flow (sequential, no buy) ===");
        Console.WriteLine($"Trade profile: {RaceProfileManager.CurrentTradeProfile}");

        var hWnd = CliBootstrap.FindGameOrLogError();
        if (hWnd == IntPtr.Zero)
            return 1;

        using var capture = new BitBltCapture();
        capture.Start(hWnd);
        using var cts = new CancellationTokenSource();
        using var ctx = new GameContext(hWnd, capture, cts.Token);

        var executor = new DefaultTradeFlowExecutor(validationOnly: true);
        TradeExecutionResult result = await executor.ExecuteAsync(ctx);
        Console.WriteLine($"Trade flow validation result: {result}");
        return 0;
    }
}
