using System.Diagnostics;
using OpenCvSharp;
using SleepRunner.Recognition;
using SleepRunner.Utils;

namespace SleepRunner.Vision;

/// <summary>
/// 单帧识别上下文：在一次截屏的生命周期内，缓存 OCR 结果，避免多个 Handler 重复调用
///
/// 设计意图：
/// - 跑马主循环每 tick 截一张 Mat，依次过 16 个 Handler 的 CanHandle/DescribeDecision
/// - 多个 Handler 经常对同一区域跑同一份 OCR（典型重复）
/// - 把"这一帧已经算过的结果"按 RegionKey 缓存下来，命中即直接返回
/// - Handler 仍然可以直接拿 Screenshot 走老路，渐进迁移
///
/// 不持有 Mat 所有权 —— Mat 由调用方 using/dispose
/// </summary>
public sealed class FrameContext
{
    /// <summary>当前帧的截屏 Mat（不拥有 dispose 权）</summary>
    public Mat Screenshot { get; }

    private readonly Dictionary<string, string> _ocrCache = new();
    private readonly Dictionary<string, IReadOnlyList<OcrHelper.OcrLineHit>> _ocrLinesCache = new();

    private readonly List<TraceEntry> _trace = new();
    private readonly Stopwatch _frameSw = Stopwatch.StartNew();

    public FrameContext(Mat screenshot)
    {
        Screenshot = screenshot;
    }

    #region OCR 缓存

    /// <summary>
    /// 缓存版的 OcrHelper.RecognizeRegion：相同 ROI 在同一帧内只跑一次 OCR
    /// </summary>
    public async Task<string> GetOcrAsync(double xPct, double yPct, double wPct, double hPct, string? label = null)
    {
        var key = RegionKey.Make(xPct, yPct, wPct, hPct);
        if (_ocrCache.TryGetValue(key, out var cached))
        {
            _trace.Add(new TraceEntry(label ?? $"OCR {key}", 0, true));
            return cached;
        }

        var sw = Stopwatch.StartNew();
        var text = await OcrHelper.RecognizeRegion(Screenshot, xPct, yPct, wPct, hPct);
        sw.Stop();

        _ocrCache[key] = text;
        _trace.Add(new TraceEntry(label ?? $"OCR {key}", sw.Elapsed.TotalMilliseconds, false));
        return text;
    }

    /// <summary>
    /// 缓存版的 OcrHelper.RecognizeRegionLines：相同 ROI 在同一帧内只跑一次行级 OCR
    /// </summary>
    public async Task<IReadOnlyList<OcrHelper.OcrLineHit>> GetOcrLinesAsync(double xPct, double yPct, double wPct, double hPct, string? label = null)
    {
        var key = "L:" + RegionKey.Make(xPct, yPct, wPct, hPct);
        if (_ocrLinesCache.TryGetValue(key, out var cached))
        {
            _trace.Add(new TraceEntry(label ?? $"OCRLINES {key}", 0, true));
            return cached;
        }

        var sw = Stopwatch.StartNew();
        var lines = await OcrHelper.RecognizeRegionLines(Screenshot, xPct, yPct, wPct, hPct);
        sw.Stop();

        _ocrLinesCache[key] = lines;
        _trace.Add(new TraceEntry(label ?? $"OCRLINES {key}", sw.Elapsed.TotalMilliseconds, false));
        return lines;
    }

    #endregion

    #region 性能跟踪

    /// <summary>
    /// 整帧耗时若超过阈值则把 Top-N 慢调用打印出来，定位拖慢决策的具体 OCR
    /// </summary>
    public void DumpIfSlow(double thresholdMs = 800, string moduleTag = "Frame")
    {
        var totalMs = _frameSw.Elapsed.TotalMilliseconds;
        if (totalMs < thresholdMs) return;

        Logger.Log($"[{moduleTag}] Frame slow: total={totalMs:F0}ms, ocrHits={_ocrCache.Count}, lineHits={_ocrLinesCache.Count}");

        var top = _trace
            .GroupBy(t => t.Label)
            .Select(g => (Label: g.Key, Ms: g.Sum(t => t.Ms), Hits: g.Count(t => t.CacheHit), Total: g.Count()))
            .OrderByDescending(t => t.Ms)
            .Take(8);

        foreach (var (label, ms, hits, total) in top)
            Logger.Log($"[{moduleTag}]   {label} x{total} (cache_hit={hits}) = {ms:F0}ms");
    }

    private readonly record struct TraceEntry(string Label, double Ms, bool CacheHit);

    #endregion
}
