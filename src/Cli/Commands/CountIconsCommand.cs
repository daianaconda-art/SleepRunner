using OpenCvSharp;
using SleepRunner.Automation.Race.Handlers;
using SleepRunner.Automation.Race.Handlers.Training;

namespace SleepRunner.Cli.Commands;

/// <summary>
/// --count-icons &lt;file&gt;：统计圆形图标数 + 输出失败率红色标记行
/// </summary>
internal sealed class CountIconsCommand : ICliCommand
{
    public string Name => "--count-icons";

    public Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: --count-icons <file>");
            return Task.FromResult(1);
        }

        string filePath = args[1];
        Console.WriteLine($"=== Count Icons: {filePath} ===");
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"ERROR: File not found: {filePath}");
            return Task.FromResult(1);
        }

        using var img = Cv2.ImRead(filePath);
        if (img.Empty())
        {
            Console.WriteLine("ERROR: Failed to load image");
            return Task.FromResult(1);
        }

        Console.WriteLine($"Image: {img.Width}x{img.Height}");
        int count = TrainingSelectHandler.CountCircularIcons(img);
        Console.WriteLine($"Circular icons detected: {count}");

        int selected = TrainingFailRateOcr.DetectSelectedOption(img, null);
        Console.WriteLine(selected >= 0
            ? $"Fail rate marker row: {selected + 1}"
            : "Fail rate marker: NOT found");
        return Task.FromResult(0);
    }
}
