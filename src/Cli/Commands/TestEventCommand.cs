using OpenCvSharp;
using SleepRunner.Automation.Race;
using SleepRunner.Capture;
using SleepRunner.Recognition;
using SleepRunner.Utils;

namespace SleepRunner.Cli.Commands;

/// <summary>
/// --test-event [file]：识别事件标题/选项 + 在 race_events.json 中匹配并打印命中事件
/// </summary>
internal sealed class TestEventCommand : ICliCommand
{
    public string Name => "--test-event";

    public async Task<int> ExecuteAsync(string[] args)
    {
        Console.WriteLine("=== Event Handler Test ===");
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

        // OCR 事件标题
        Console.WriteLine("\n--- Event title (x=0.01 y=0.07 w=0.22 h=0.12) ---");
        string titleRaw = await OcrHelper.RecognizeRegion(img, 0.01, 0.07, 0.22, 0.12);
        string title = titleRaw.Replace(" ", "").Replace("\r", "").Replace("\n", "").Trim();
        Console.WriteLine($"Raw: {titleRaw}");
        Console.WriteLine($"Normalized: {title}");

        // OCR 选项
        Console.WriteLine("\n--- Options (x=0.55 y=0.45 w=0.40 h=0.35) ---");
        string optRaw = await OcrHelper.RecognizeRegion(img, 0.55, 0.45, 0.40, 0.35);
        string options = optRaw.Replace(" ", "").Replace("\r", "").Replace("\n", "").Trim();
        Console.WriteLine($"Raw: {optRaw}");
        Console.WriteLine($"Normalized: {options}");

        // 加载 JSON 并匹配
        var jsonPath = Path.Combine(PathHelper.BaseDir, "assets", "race_events.json");
        Console.WriteLine($"\n--- Matching against {jsonPath} ---");
        if (!File.Exists(jsonPath))
        {
            Console.WriteLine("ERROR: race_events.json not found");
            img.Dispose();
            return 1;
        }

        var data = System.Text.Json.JsonSerializer.Deserialize<EventDecisionData>(
            File.ReadAllText(jsonPath));
        if (data == null)
        {
            Console.WriteLine("ERROR: Failed to parse JSON");
            img.Dispose();
            return 1;
        }

        Console.WriteLine($"Loaded {data.Events.Count} events");

        // 按事件名匹配
        RaceEvent? matched = null;
        foreach (var evt in data.Events)
        {
            string name = evt.EventName.Replace(" ", "");
            if (!string.IsNullOrEmpty(name) && title.Contains(name))
            {
                matched = evt;
                Console.WriteLine($"TITLE MATCH: [{evt.Id}] {evt.EventName}");
                break;
            }
        }

        // 按关键词匹配
        if (matched == null)
        {
            int bestScore = 0;
            foreach (var evt in data.Events)
            {
                int score = 0;
                foreach (var opt in evt.Options)
                {
                    string kw = opt.Keyword.Replace(" ", "");
                    if (!string.IsNullOrEmpty(kw) && options.Contains(kw))
                    {
                        score += 3;
                        continue;
                    }
                    foreach (var alias in opt.Alias)
                    {
                        string a = alias.Replace(" ", "");
                        if (!string.IsNullOrEmpty(a) && options.Contains(a))
                        {
                            score += 1;
                            break;
                        }
                    }
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    matched = evt;
                }
            }
            if (matched != null && bestScore >= 2)
                Console.WriteLine($"KEYWORD MATCH: [{matched.Id}] {matched.EventName} (score={bestScore})");
            else
                matched = null;
        }

        if (matched == null)
        {
            Console.WriteLine("NO MATCH FOUND - unknown event");
        }
        else
        {
            Console.WriteLine($"\nResult:");
            Console.WriteLine($"  Event: {matched.EventName}");
            Console.WriteLine($"  Status: {matched.Status}");
            Console.WriteLine($"  Recommended: option {matched.RecommendedOption?.ToString() ?? "null"}");
            Console.WriteLine($"  Note: {matched.Note}");

            if (matched.RecommendedOption != null)
            {
                int total = matched.Options.Count;
                int idx = matched.RecommendedOption.Value;
                double clickY = 0.73 - (total - idx) * 0.08;
                Console.WriteLine($"  Would click at: ({0.75:F2}, {clickY:F3})");
            }
        }

        img.Dispose();
        Console.WriteLine("\nTest done.");
        return 0;
    }
}
