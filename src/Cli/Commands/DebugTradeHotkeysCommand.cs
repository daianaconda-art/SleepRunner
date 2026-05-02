using SleepRunner.Automation;
using SleepRunner.Automation.Race.Handlers.Trade;
using SleepRunner.Capture;
using SleepRunner.Utils;

namespace SleepRunner.Cli.Commands;

/// <summary>
/// --debug-trade-hotkeys: current trade screen only; sends Alt+1/2/3 and logs detail OCR without buying.
/// </summary>
internal sealed class DebugTradeHotkeysCommand : ICliCommand
{
    public string Name => "--debug-trade-hotkeys";

    public async Task<int> ExecuteAsync(string[] args)
    {
        CliBootstrap.EnsureUtf8Console();
        var settings = UserSettings.Load();
        settings.ApplyToRaceConfig();

        Console.WriteLine("=== Debug Trade Hotkeys (Alt+1/2/3 only, no buy) ===");
        Console.WriteLine("Keep the game on the trade screen with the three-item list visible.");

        var hWnd = CliBootstrap.FindGameOrLogError();
        if (hWnd == IntPtr.Zero)
            return 1;

        using var capture = new BitBltCapture();
        capture.Start(hWnd);
        using var cts = new CancellationTokenSource();
        using var ctx = new GameContext(hWnd, capture, cts.Token);

        await TradeHotkeyProbe.RunAsync(ctx);
        Console.WriteLine("Trade hotkey probe finished. Check latest.log for [Race:TradeHotkeys] lines.");
        return 0;
    }
}
