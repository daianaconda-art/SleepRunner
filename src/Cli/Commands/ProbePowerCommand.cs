using OpenCvSharp;
using SleepRunner.Automation.Race.Handlers;
using SleepRunner.Capture;
using SleepRunner.Utils;

namespace SleepRunner.Cli.Commands;

/// <summary>
/// --probe-power [file]：探测当前画面或指定截图中的力量值 OCR
/// </summary>
internal sealed class ProbePowerCommand : ICliCommand
{
    public string Name => "--probe-power";

    public async Task<int> ExecuteAsync(string[] args)
    {
        CliBootstrap.EnsureUtf8Console();
        Console.WriteLine("=== Probe Power Stat ===");
        string? filePath = args.Length >= 2 ? args[1] : null;

        Mat? img = null;
        if (!string.IsNullOrEmpty(filePath))
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"ERROR: File not found: {filePath}");
                return 1;
            }

            img = Cv2.ImRead(filePath);
            if (img.Empty())
            {
                Console.WriteLine($"ERROR: Failed to read image: {filePath}");
                img.Dispose();
                return 1;
            }
        }
        else
        {
            var hWnd = CliBootstrap.FindGameOrLogError();
            if (hWnd == IntPtr.Zero)
                return 1;

            using var capture = new BitBltCapture();
            capture.Start(hWnd);
            img = capture.Capture();
            if (img == null || img.Empty())
            {
                Console.WriteLine("ERROR: Capture failed.");
                img?.Dispose();
                return 1;
            }

            Directory.CreateDirectory(PathHelper.ScreenshotsDir);
            string outPath = Path.Combine(PathHelper.ScreenshotsDir, $"probe_power_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            Cv2.ImWrite(outPath, img);
            Console.WriteLine($"Captured screenshot: {outPath}");
        }

        try
        {
            int power = await TrainingSelectHandler.DebugReadPowerStatAsync(img);
            Console.WriteLine(power >= 0 ? $"Detected power stat: {power}" : "Detected power stat: N/A");
        }
        finally
        {
            img.Dispose();
        }
        return 0;
    }
}
