using OpenCvSharp;
using SleepRunner.Capture;
using SleepRunner.Input;
using SleepRunner.Utils;

namespace SleepRunner.Cli.Commands;

/// <summary>
/// --scroll &lt;xPct&gt; &lt;yPct&gt; &lt;clicks&gt; [waitMs]：在指定位置触发滚轮
/// </summary>
internal sealed class ScrollCommand : ICliCommand
{
    public string Name => "--scroll";

    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length < 4)
        {
            Console.WriteLine("Usage: --scroll <xPct> <yPct> <clicks> [waitMs]");
            return 1;
        }

        double xp = double.Parse(args[1]);
        double yp = double.Parse(args[2]);
        int clicks = int.Parse(args[3]);
        int waitMs = args.Length >= 5 ? int.Parse(args[4]) : 2000;

        var hWnd = CliBootstrap.FindGameOrLogNotRunning();
        if (hWnd == IntPtr.Zero)
            return 1;

        var (w, h) = WindowHelper.GetClientSize(hWnd);
        float dpi = MouseSimulator.GetDpiScale();
        int x = (int)(w * dpi * xp);
        int y = (int)(h * dpi * yp);
        await MouseSimulator.ScrollAtClient(hWnd, x, y, clicks);
        await Task.Delay(waitMs);

        using var capture = new BitBltCapture();
        capture.Start(hWnd);
        using var shot = capture.Capture();
        if (shot != null && !shot.Empty())
        {
            Directory.CreateDirectory(PathHelper.ScreenshotsDir);
            var path = Path.Combine(PathHelper.ScreenshotsDir, $"scroll_{DateTime.Now:HHmmss}.png");
            Cv2.ImWrite(path, shot);
            Console.WriteLine($"Screenshot: {path}");
        }
        return 0;
    }
}
