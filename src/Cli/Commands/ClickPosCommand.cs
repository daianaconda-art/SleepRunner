using OpenCvSharp;
using SleepRunner.Capture;
using SleepRunner.Input;
using SleepRunner.Utils;

namespace SleepRunner.Cli.Commands;

/// <summary>
/// --clickpos &lt;xPct&gt; &lt;yPct&gt; [waitMs]：按窗口百分比点击 + 截图
/// </summary>
internal sealed class ClickPosCommand : ICliCommand
{
    public string Name => "--clickpos";

    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: --clickpos <xPct> <yPct> [waitMs]");
            return 1;
        }

        double xp = double.Parse(args[1]);
        double yp = double.Parse(args[2]);
        int waitMs = args.Length >= 4 ? int.Parse(args[3]) : 2000;

        var hWnd = CliBootstrap.FindGameOrLogNotRunning();
        if (hWnd == IntPtr.Zero)
            return 1;

        var (w, h) = WindowHelper.GetClientSize(hWnd);
        float dpi = MouseSimulator.GetDpiScale();
        int x = (int)(w * dpi * xp);
        int y = (int)(h * dpi * yp);
        Console.WriteLine($"Click ({x},{y}) physical in {w}x{h} logical, dpi={dpi:F2}");

        await MouseSimulator.ClickAtClient(hWnd, x, y);
        await Task.Delay(waitMs);

        using var capture = new BitBltCapture();
        capture.Start(hWnd);
        using var shot = capture.Capture();
        if (shot != null && !shot.Empty())
        {
            Directory.CreateDirectory(PathHelper.ScreenshotsDir);
            var path = Path.Combine(PathHelper.ScreenshotsDir, $"clickpos_{DateTime.Now:HHmmss}.png");
            Cv2.ImWrite(path, shot);
            Console.WriteLine($"Screenshot: {path}");
        }
        return 0;
    }
}
