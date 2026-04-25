using OpenCvSharp;
using SleepRunner.Capture;
using SleepRunner.Recognition;

namespace SleepRunner.Cli.Commands;

/// <summary>
/// --probe-event-y [file]：对事件页选项区做行级 OCR，输出每行真实 Y（用于校准 option_y_overrides）
/// </summary>
internal sealed class ProbeEventYCommand : ICliCommand
{
    public string Name => "--probe-event-y";

    public async Task<int> ExecuteAsync(string[] args)
    {
        string? filePath = args.Length >= 2 ? args[1] : null;

        Mat? img;
        if (filePath != null)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"ERROR: File not found: {filePath}");
                return 1;
            }
            img = Cv2.ImRead(filePath);
        }
        else
        {
            var hWnd = CliBootstrap.FindGameOrLogNotRunning();
            if (hWnd == IntPtr.Zero)
                return 1;
            using var capture = new BitBltCapture();
            capture.Start(hWnd);
            img = capture.Capture();
        }
        if (img == null || img.Empty())
        {
            Console.WriteLine("ERROR: No image");
            return 1;
        }
        Console.WriteLine($"Image: {img.Width}x{img.Height}");

        // 扫"右半屏 + 中下"大区，覆盖底部条目和右下气泡两种布局
        double rx = 0.50, ry = 0.40, rw = 0.50, rh = 0.55;
        Console.WriteLine($"\n=== ROI=(x={rx} y={ry} w={rw} h={rh}) line-level OCR ===");
        var lines = await OcrHelper.RecognizeRegionLines(img, rx, ry, rw, rh);
        Console.WriteLine($"Found {lines.Count} lines:");
        foreach (var hit in lines.OrderBy(l => l.CenterYPct))
        {
            // 行中心相对 ROI 的百分比 -> 转换为相对整张截图的百分比
            double absX = rx + hit.CenterXPct * rw;
            double absY = ry + hit.CenterYPct * rh;
            int px = (int)(img.Width * absX);
            int py = (int)(img.Height * absY);
            Console.WriteLine($"  Y={absY:F3} (X={absX:F3}) px=({px},{py})  '{hit.Text}'");
        }
        img.Dispose();
        Console.WriteLine("\nProbe done.");
        return 0;
    }
}
