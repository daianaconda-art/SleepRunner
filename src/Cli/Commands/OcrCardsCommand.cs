using System.Text.RegularExpressions;
using OpenCvSharp;
using SleepRunner.Capture;
using SleepRunner.Recognition;

namespace SleepRunner.Cli.Commands;

/// <summary>
/// --ocr-cards [file]：识别三选一卡面文本，仅输出，不点击
/// </summary>
internal sealed class OcrCardsCommand : ICliCommand
{
    public string Name => "--ocr-cards";

    public async Task<int> ExecuteAsync(string[] args)
    {
        Console.WriteLine("=== Card OCR (3 choices, no click) ===");
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

        var regions = new (double X, double Y, double W, double H)[]
        {
            (0.06, 0.26, 0.27, 0.38),
            (0.365, 0.26, 0.27, 0.38),
            (0.67, 0.26, 0.27, 0.38),
        };

        for (int i = 0; i < regions.Length; i++)
        {
            var r = regions[i];
            string raw = await OcrHelper.RecognizeRegion(img, r.X, r.Y, r.W, r.H);
            string norm = NormalizeCardOcr(raw);
            Console.WriteLine($"Card[{i + 1}] raw: {raw}");
            Console.WriteLine($"Card[{i + 1}] normalized: {norm}");
        }

        img.Dispose();
        Console.WriteLine("Card OCR done.");
        return 0;
    }

    /// <summary>
    /// 卡面 OCR 文本标准化，便于白名单关键词录入
    /// </summary>
    private static string NormalizeCardOcr(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        return Regex.Replace(raw, @"[\s\u3000]+", "").Trim();
    }
}
