using SleepRunner.Automation;
using SleepRunner.Automation.Race.Handlers.Trade;
using SleepRunner.Capture;
using SleepRunner.Automation.Race.Policy;
using SleepRunner.Utils;
using OpenCvSharp;

namespace SleepRunner.Cli.Commands;

/// <summary>
/// --debug-trade：交易页扫描诊断（不购买），打印识别结果与购买队列
/// </summary>
internal sealed class DebugTradeCommand : ICliCommand
{
    public string Name => "--debug-trade";

    public async Task<int> ExecuteAsync(string[] args)
    {
        CliBootstrap.EnsureUtf8Console();
        var settings = UserSettings.Load();
        settings.ApplyToRaceConfig();

        Console.WriteLine("=== Debug Trade (scan only, no buy) ===");
        Console.WriteLine($"Trade profile: {RaceProfileManager.CurrentTradeProfile}");

        var hWnd = CliBootstrap.FindGameOrLogError();
        if (hWnd == IntPtr.Zero)
            return 1;

        using var capture = new BitBltCapture();
        capture.Start(hWnd);
        using var cts = new CancellationTokenSource();
        using var ctx = new GameContext(hWnd, capture, cts.Token);

        using (var shot = ctx.CaptureScreen())
        {
            if (shot != null && !shot.Empty())
                DumpPriceRois(shot);
        }

        await DefaultTradeFlowExecutor.DebugScanAsync(ctx);
        return 0;
    }

    private static void DumpPriceRois(Mat screenshot)
    {
        string dir = Path.Combine(PathHelper.LogsDir, "debug_trade_price");
        Directory.CreateDirectory(dir);

        Cv2.ImWrite(Path.Combine(dir, "full_trade.png"), screenshot);

        int idx = 1;
        foreach (var region in TradeDetailOcr.DetailPriceRegions)
        {
            SaveCrop(screenshot, region, Path.Combine(dir, $"detail_{idx}.png"));
            idx++;
        }

        for (int slot = 0; slot < TradeDetailOcr.OfferSlots.Length; slot++)
        {
            int regionIdx = 1;
            foreach (var region in TradeDetailOcr.GetSlotPriceRegions(slot))
            {
                SaveCrop(screenshot, region, Path.Combine(dir, $"slot{slot + 1}_price_{regionIdx}.png"));
                regionIdx++;
            }
        }

        Console.WriteLine($"Saved trade price ROI debug crops to: {dir}");
    }

    private static void SaveCrop(Mat screenshot, (double X, double Y, double W, double H) region, string path)
    {
        var rect = TradeDetailOcr.ToPixelRect(screenshot, region.X, region.Y, region.W, region.H);
        using var roi = new Mat(screenshot, rect);
        Cv2.ImWrite(path, roi);
    }
}
