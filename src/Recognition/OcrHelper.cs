using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using OpenCvSharp;
using SleepRunner.Utils;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace SleepRunner.Recognition;

/// <summary>
/// 基于 Windows 内置 OCR 的文字识别工具（支持中文简体）
/// </summary>
public static class OcrHelper
{
    private static OcrEngine? _engine;
    private static readonly Dictionary<string, OcrEngine?> _engineByLanguage = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 识别 Mat 图像中指定区域的文字
    /// </summary>
    public static async Task<string> RecognizeRegion(Mat screenshot,
        double xPct, double yPct, double wPct, double hPct)
    {
        int x = (int)(screenshot.Width * xPct);
        int y = (int)(screenshot.Height * yPct);
        int w = (int)(screenshot.Width * wPct);
        int h = (int)(screenshot.Height * hPct);

        x = Math.Clamp(x, 0, screenshot.Width - 1);
        y = Math.Clamp(y, 0, screenshot.Height - 1);
        w = Math.Min(w, screenshot.Width - x);
        h = Math.Min(h, screenshot.Height - y);

        if (w <= 0 || h <= 0) return string.Empty;

        using var region = new Mat(screenshot, new Rect(x, y, w, h));
        using var regionClone = region.Clone();
        return await RecognizeMat(regionClone);
    }

    /// <summary>
    /// 识别整张 Mat 图像中的文字
    /// </summary>
    public static async Task<string> RecognizeMat(Mat mat)
    {
        return await RecognizeMat(mat, null);
    }

    public static async Task<string> RecognizeMat(Mat mat, string? languageTag)
    {
        var engine = GetEngine(languageTag);
        if (engine == null) return "[OCR engine unavailable]";

        using var safeMat = mat.Clone();
        using var bgramat = new Mat();
        if (safeMat.Channels() == 3)
            Cv2.CvtColor(safeMat, bgramat, ColorConversionCodes.BGR2BGRA);
        else if (safeMat.Channels() == 4)
            safeMat.CopyTo(bgramat);
        else
            return "[Unsupported image format]";

        var bitmap = await MatToSoftwareBitmap(bgramat);
        if (bitmap == null) return "[Bitmap conversion failed]";

        var result = await engine.RecognizeAsync(bitmap);
        return result.Text ?? string.Empty;
    }

    /// <summary>
    /// 单行 OCR 结果（在指定截图百分比坐标系下）
    /// CenterYPct/CenterXPct 是该行在 ROI 内整体的中心位置百分比（基于 ROI 宽高），
    /// 调用方需要再叠加 ROI 的 (xPct,yPct,wPct,hPct) 才能换算回截图坐标。
    /// </summary>
    public readonly record struct OcrLineHit(string Text, double CenterXPct, double CenterYPct, double WidthPct, double HeightPct);

    /// <summary>
    /// 识别 Mat 区域中的文字，返回按行切分的结果，每行带相对 ROI 的中心 Y/X
    /// 用于：休息选项、训练菜单等"按行定位"的场景，避免反复 OCR 多个小窗口
    /// </summary>
    public static async Task<List<OcrLineHit>> RecognizeRegionLines(Mat screenshot,
        double xPct, double yPct, double wPct, double hPct)
    {
        var hits = new List<OcrLineHit>();

        int x = (int)(screenshot.Width * xPct);
        int y = (int)(screenshot.Height * yPct);
        int w = (int)(screenshot.Width * wPct);
        int h = (int)(screenshot.Height * hPct);

        x = Math.Clamp(x, 0, screenshot.Width - 1);
        y = Math.Clamp(y, 0, screenshot.Height - 1);
        w = Math.Min(w, screenshot.Width - x);
        h = Math.Min(h, screenshot.Height - y);
        if (w <= 0 || h <= 0) return hits;

        _engine ??= CreateEngine();
        if (_engine == null) return hits;

        using var region = new Mat(screenshot, new Rect(x, y, w, h));
        using var bgramat = new Mat();
        if (region.Channels() == 3)
            Cv2.CvtColor(region, bgramat, ColorConversionCodes.BGR2BGRA);
        else if (region.Channels() == 4)
            region.CopyTo(bgramat);
        else
            return hits;

        var bitmap = await MatToSoftwareBitmap(bgramat);
        if (bitmap == null) return hits;

        var result = await _engine.RecognizeAsync(bitmap);
        if (result?.Lines == null) return hits;

        foreach (var line in result.Lines)
        {
            if (line.Words == null || line.Words.Count == 0)
            {
                if (string.IsNullOrEmpty(line.Text)) continue;
                hits.Add(new OcrLineHit(line.Text, 0.5, 0.5, 1.0, 0.0));
                continue;
            }

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var word in line.Words)
            {
                var br = word.BoundingRect;
                if (br.X < minX) minX = br.X;
                if (br.Y < minY) minY = br.Y;
                if (br.X + br.Width > maxX) maxX = br.X + br.Width;
                if (br.Y + br.Height > maxY) maxY = br.Y + br.Height;
            }

            if (minX >= maxX || minY >= maxY) continue;

            double cxPct = (minX + maxX) * 0.5 / w;
            double cyPct = (minY + maxY) * 0.5 / h;
            double wPctLine = (maxX - minX) / w;
            double hPctLine = (maxY - minY) / h;
            hits.Add(new OcrLineHit(line.Text ?? "", cxPct, cyPct, wPctLine, hPctLine));
        }

        return hits;
    }

    private static OcrEngine? CreateEngine()
    {
        var lang = new Language("zh-Hans");
        var engine = OcrEngine.TryCreateFromLanguage(lang);
        if (engine != null)
        {
            Logger.Log("[OCR] Engine created: zh-Hans");
            return engine;
        }

        engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine != null)
        {
            Logger.Log($"[OCR] Engine created from user profile: {engine.RecognizerLanguage.LanguageTag}");
            return engine;
        }

        Logger.Log("[OCR] ERROR: No suitable OCR engine found");
        return null;
    }

    private static OcrEngine? GetEngine(string? languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag))
        {
            _engine ??= CreateEngine();
            return _engine;
        }

        if (_engineByLanguage.TryGetValue(languageTag, out var cached))
            return cached;

        try
        {
            var lang = new Language(languageTag);
            var engine = OcrEngine.TryCreateFromLanguage(lang);
            if (engine != null)
                Logger.Log($"[OCR] Engine created: {languageTag}");
            else
                Logger.Log($"[OCR] Engine unavailable: {languageTag}");
            _engineByLanguage[languageTag] = engine;
            return engine;
        }
        catch (Exception ex)
        {
            Logger.Log($"[OCR] Engine create failed for {languageTag}: {ex.Message}");
            _engineByLanguage[languageTag] = null;
            return null;
        }
    }

    private static Task<SoftwareBitmap?> MatToSoftwareBitmap(Mat bgraMat)
    {
        int w = bgraMat.Width;
        int h = bgraMat.Height;
        int stride = w * 4;

        var bytes = new byte[h * stride];
        Marshal.Copy(bgraMat.Data, bytes, 0, bytes.Length);

        var bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, w, h, BitmapAlphaMode.Premultiplied);
        bitmap.CopyFromBuffer(bytes.AsBuffer());
        return Task.FromResult<SoftwareBitmap?>(bitmap);
    }
}
