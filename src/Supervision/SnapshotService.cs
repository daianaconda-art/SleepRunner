using OpenCvSharp;
using SleepRunner.Capture;
using SleepRunner.Utils;

namespace SleepRunner.Supervision;

/// <summary>
/// 监督层复用的窗口快照能力。
/// </summary>
public static class SnapshotService
{
    public static string CaptureGameSnapshot(string? outputPath = null, string filePrefix = "snapshot")
    {
        var hWnd = WindowHelper.FindTargetWindow();
        if (hWnd == IntPtr.Zero)
            throw new InvalidOperationException("Target window not found.");

        using var capture = new BitBltCapture();
        capture.Start(hWnd);

        using var shot = capture.Capture();
        if (shot == null || shot.Empty())
            throw new InvalidOperationException("Capture failed.");

        string path = outputPath ?? BuildDefaultOutputPath(filePrefix);
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (!Cv2.ImWrite(path, shot))
            throw new InvalidOperationException($"Failed to write snapshot: {path}");

        Logger.Log($"[Supervision] Snapshot saved: {path}");
        return path;
    }

    private static string BuildDefaultOutputPath(string filePrefix)
    {
        Directory.CreateDirectory(PathHelper.SupervisionSnapshotsDir);
        string safePrefix = SanitizeFileName(filePrefix);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return Path.Combine(PathHelper.SupervisionSnapshotsDir, $"{timestamp}_{safePrefix}.png");
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "snapshot";

        var chars = value.Select(ch =>
        {
            if (char.IsLetterOrDigit(ch))
                return ch;
            return ch is '-' or '_' ? ch : '_';
        }).ToArray();

        return new string(chars).Trim('_');
    }
}
