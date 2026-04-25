using OpenCvSharp;
using SleepRunner.Capture;
using SleepRunner.Utils;

namespace SleepRunner.Cli.Commands;

/// <summary>
/// --test: 截图并保存，验证窗口捕获链路
/// </summary>
internal sealed class TestCommand : ICliCommand
{
    public string Name => "--test";

    public Task<int> ExecuteAsync(string[] args)
    {
        Console.WriteLine("=== SleepRunner CLI Test Mode ===");
        Console.WriteLine($"Base dir: {PathHelper.BaseDir}");

        var hWnd = CliBootstrap.FindGameOrLogNotRunning();
        if (hWnd == IntPtr.Zero)
            return Task.FromResult(1);

        var (w, h) = WindowHelper.GetClientSize(hWnd);
        Console.WriteLine($"Game found: {w}x{h}");

        using var capture = new BitBltCapture();
        capture.Start(hWnd);

        using var shot = capture.Capture();
        if (shot == null || shot.Empty())
        {
            Console.WriteLine("ERROR: Capture failed");
            return Task.FromResult(1);
        }
        Console.WriteLine($"Capture OK: {shot.Width}x{shot.Height}");

        Directory.CreateDirectory(PathHelper.ScreenshotsDir);
        var screenshotPath = Path.Combine(PathHelper.ScreenshotsDir, $"cli_test_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        Cv2.ImWrite(screenshotPath, shot);
        Console.WriteLine($"Screenshot saved: {screenshotPath}");

        Console.WriteLine("CLI test done.");
        return Task.FromResult(0);
    }
}
