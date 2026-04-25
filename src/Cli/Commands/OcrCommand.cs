using OpenCvSharp;
using SleepRunner.Capture;
using SleepRunner.Recognition;
using SleepRunner.Utils;

namespace SleepRunner.Cli.Commands;

/// <summary>
/// --ocr [file]：右侧选项区 + 底部叙事区 OCR
/// </summary>
internal sealed class OcrCommand : ICliCommand
{
    public string Name => "--ocr";

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

        // 右侧对话选项区域
        Console.WriteLine("\n=== Right side options (y=0.62~0.78) ===");
        string rightText = await OcrHelper.RecognizeRegion(img, 0.55, 0.62, 0.40, 0.16);
        Console.WriteLine(rightText);

        // 底部叙事文字
        Console.WriteLine("\n=== Bottom narration (y=0.78~0.92) ===");
        string bottomText = await OcrHelper.RecognizeRegion(img, 0.10, 0.78, 0.80, 0.14);
        Console.WriteLine(bottomText);

        img.Dispose();
        Console.WriteLine("\nOCR done.");
        return 0;
    }
}
