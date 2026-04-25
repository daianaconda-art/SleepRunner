using OpenCvSharp;
using SleepRunner.Automation.Race.Handlers;
using SleepRunner.Capture;
using SleepRunner.Utils;

namespace SleepRunner.Cli.Commands;

/// <summary>
/// --locate-commission-skip [file]：定位委托弹窗"跳过战斗"按钮位置（视觉），输出位置 + 调试图
/// </summary>
internal sealed class LocateCommissionSkipCommand : ICliCommand
{
    public string Name => "--locate-commission-skip";

    public Task<int> ExecuteAsync(string[] args)
    {
        CliBootstrap.EnsureUtf8Console();
        Console.WriteLine("=== Locate Commission Skip Button ===");
        string? filePath = args.Length >= 2 ? args[1] : null;

        Mat? img = null;
        if (!string.IsNullOrEmpty(filePath))
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"ERROR: File not found: {filePath}");
                return Task.FromResult(1);
            }
            img = Cv2.ImRead(filePath);
        }
        else
        {
            var hWnd = CliBootstrap.FindGameOrLogError();
            if (hWnd == IntPtr.Zero)
                return Task.FromResult(1);

            using var capture = new BitBltCapture();
            capture.Start(hWnd);
            img = capture.Capture();
        }

        if (img == null || img.Empty())
        {
            Console.WriteLine("ERROR: No image");
            return Task.FromResult(1);
        }

        Console.WriteLine($"Image: {img.Width}x{img.Height}");
        if (!CommissionPopupLocator.TryLocateSkipButton(img, out var rect, out var center, out double blueRatio))
        {
            Console.WriteLine("Skip button NOT found.");
            img.Dispose();
            return Task.FromResult(0);
        }

        double xPct = center.X / (double)img.Width;
        double yPct = center.Y / (double)img.Height;
        Console.WriteLine($"Skip rect: x={rect.X}, y={rect.Y}, w={rect.Width}, h={rect.Height}");
        Console.WriteLine($"Skip center: ({center.X},{center.Y}) => ({xPct:F6},{yPct:F6})");
        Console.WriteLine($"Blue fill ratio: {blueRatio:F3}");

        using var debug = img.Clone();
        Cv2.Rectangle(debug, rect, Scalar.Lime, 4);
        Cv2.Circle(debug, center, 10, Scalar.Red, 4);
        Directory.CreateDirectory(PathHelper.ScreenshotsDir);
        string outPath = Path.Combine(PathHelper.ScreenshotsDir, $"commission_skip_locate_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        Cv2.ImWrite(outPath, debug);
        Console.WriteLine($"Debug saved: {outPath}");

        img.Dispose();
        return Task.FromResult(0);
    }
}
