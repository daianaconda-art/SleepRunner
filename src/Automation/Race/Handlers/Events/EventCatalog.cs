using System.Text.Json;
using SleepRunner.Automation.Race.Policy;
using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Handlers.Events;

/// <summary>
/// 事件决策数据加载与匹配（按 RaceProfileManager.CurrentEventsProfile 选 JSON）
///
/// 拆分意图：
/// - 把 events/*.json 的加载/反序列化/匹配逻辑独立出来
/// - profile 切换或文件 mtime 变化都会让下一次访问自动重读
/// - 解析异常时保留上一次有效数据，避免坏 JSON 把跑马打瘫
/// </summary>
internal sealed class EventCatalog
{
    private EventDecisionData? _decisionData;
    private DateTime _fileMtimeUtc = DateTime.MinValue;
    private DateTime _lastCheckUtc = DateTime.MinValue;
    private string _loadedPath = string.Empty;
    private static readonly TimeSpan MtimeCheckInterval = TimeSpan.FromSeconds(2);

    public EventCatalog()
    {
        // profile 切换时强制下一次 EnsureLoaded 重读
        RaceProfileManager.EventsProfileChanged += _ => InvalidateCache();
    }

    public bool IsReady => _decisionData != null && _decisionData.Events.Count > 0;
    public int EventCount => _decisionData?.Events.Count ?? 0;

    private void InvalidateCache()
    {
        _decisionData = null;
        _fileMtimeUtc = DateTime.MinValue;
        _lastCheckUtc = DateTime.MinValue;
        _loadedPath = string.Empty;
    }

    /// <summary>
    /// 懒加载 + 热重载当前 events profile JSON：
    /// - 首次调用立即加载
    /// - 后续每 2 秒做一次 mtime 检查，文件被编辑保存后下一帧自动重读
    /// - profile 切换会立即让缓存失效
    /// </summary>
    public void EnsureLoaded()
    {
        var path = RaceProfileManager.CurrentEventsPath;

        DateTime now = DateTime.UtcNow;
        bool firstLoad = _decisionData == null;
        bool pathChanged = !string.Equals(_loadedPath, path, StringComparison.OrdinalIgnoreCase);
        if (!firstLoad && !pathChanged && now - _lastCheckUtc < MtimeCheckInterval)
            return;
        _lastCheckUtc = now;

        if (!File.Exists(path))
        {
            if (firstLoad || pathChanged)
                Log.Log($"Events profile not found: {path}");
            _loadedPath = path;
            return;
        }

        DateTime mtime;
        try { mtime = File.GetLastWriteTimeUtc(path); }
        catch { mtime = DateTime.MinValue; }

        if (!firstLoad && !pathChanged && mtime == _fileMtimeUtc)
            return;

        try
        {
            var json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<EventDecisionData>(json);
            if (parsed == null)
            {
                Log.Log("Events profile parsed to null, keep previous in-memory copy.");
                return;
            }

            _decisionData = parsed;
            _fileMtimeUtc = mtime;
            _loadedPath = path;
            Log.Log(firstLoad || pathChanged
                ? $"Events profile loaded: {_decisionData.Events.Count} events from {path}"
                : $"Events profile hot-reloaded: {_decisionData.Events.Count} events (mtime={mtime:o})");
        }
        catch (Exception ex)
        {
            // 保留上一份成功加载的数据，避免坏 JSON 中断决策
            Log.Log($"Failed to load events profile: {ex.Message} (keeping previous in-memory copy)");
        }
    }

    /// <summary>
    /// 优先按事件名匹配，失败则按选项关键词匹配；对天气-训练专项做 story 兜底
    /// </summary>
    public RaceEvent? FindMatchingEvent(string title, string optionsText, string storyText = "")
    {
        if (_decisionData == null) return null;

        string normalizedTitle = EventOcrRegions.NormalizeEventTitleForMatch(title);
        string normalizedOptions = EventOcrRegions.NormalizeOcr(optionsText);
        bool allowTitleOnlyMatch =
            string.IsNullOrEmpty(normalizedOptions) ||
            EventScreenChecks.IsEventOptionHint(normalizedOptions);

        foreach (var evt in _decisionData.Events)
        {
            string name = EventOcrRegions.NormalizeEventTitleForMatch(evt.EventName);
            if (!string.IsNullOrEmpty(name) && normalizedTitle.Contains(name))
            {
                if (!allowTitleOnlyMatch)
                {
                    Log.Log(
                        $"  Title match ignored: '{evt.EventName}' because option text is not selectable " +
                        $"('{EventScreenChecks.ClipPreview(normalizedOptions)}')");
                    break;
                }

                Log.Log($"  Title match: '{evt.EventName}'");
                return evt;
            }
        }

        var weatherTitleEvent = TryMatchWeatherChoiceEventByTitle(normalizedTitle, allowTitleOnlyMatch);
        if (weatherTitleEvent != null)
            return weatherTitleEvent;

        var weatherEvent = TryMatchWeatherTrainingEvent(optionsText, storyText);
        if (weatherEvent != null)
            return weatherEvent;

        RaceEvent? bestMatch = null;
        int bestScore = 0;

        foreach (var evt in _decisionData.Events)
        {
            int score = 0;
            foreach (var opt in evt.Options)
            {
                string kw = EventOcrRegions.NormalizeOcr(opt.Keyword);
                if (!string.IsNullOrEmpty(kw) && optionsText.Contains(kw))
                {
                    score += 3;
                    continue;
                }

                foreach (var alias in opt.Alias)
                {
                    string a = EventOcrRegions.NormalizeOcr(alias);
                    if (!string.IsNullOrEmpty(a) && optionsText.Contains(a))
                    {
                        score += 1;
                        break;
                    }
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = evt;
            }
        }

        if (bestMatch != null && bestScore >= 2)
        {
            Log.Log($"  Keyword match: '{bestMatch.EventName}' (score={bestScore})");
            return bestMatch;
        }

        return null;
    }

    private RaceEvent? TryMatchWeatherChoiceEventByTitle(string normalizedTitle, bool allowTitleOnlyMatch)
    {
        if (_decisionData == null ||
            string.IsNullOrEmpty(normalizedTitle) ||
            !allowTitleOnlyMatch)
            return null;

        if (normalizedTitle.Contains("\u66b4\u96ea", StringComparison.Ordinal) ||
            normalizedTitle.Contains("\u5927\u96ea", StringComparison.Ordinal))
        {
            var match = FindEventById("weather_snow");
            if (match != null)
            {
                Log.Log("  Weather title match: weather_snow");
                return match;
            }
        }

        return null;
    }

    private RaceEvent? FindEventById(string id)
    {
        return _decisionData?.Events.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal));
    }

    /// <summary>
    /// 天气-训练专项匹配：选项含"继续训练 + 返回住处/旅馆/休息"且 story 含浓雾/雷雨
    /// </summary>
    public RaceEvent? TryMatchWeatherTrainingEvent(string optionsText, string storyText)
    {
        if (_decisionData == null)
            return null;

        bool hasContinueTraining = optionsText.Contains("继续训练", StringComparison.Ordinal);
        bool hasReturnHome = optionsText.Contains("返回住处", StringComparison.Ordinal) ||
                             optionsText.Contains("回旅馆", StringComparison.Ordinal) ||
                             optionsText.Contains("休息", StringComparison.Ordinal);
        if (!hasContinueTraining || !hasReturnHome)
            return null;

        string story = EventOcrRegions.NormalizeEventTitleForMatch(storyText);
        if (story.Contains("浓雾", StringComparison.Ordinal))
        {
            var match = _decisionData.Events.FirstOrDefault(e => string.Equals(e.Id, "weather_fog", StringComparison.Ordinal));
            if (match != null)
            {
                Log.Log("  Story match: weather_fog");
                return match;
            }
        }

        if (story.Contains("雷雨", StringComparison.Ordinal) ||
            (story.Contains("雷", StringComparison.Ordinal) && story.Contains("雨", StringComparison.Ordinal)))
        {
            var match = _decisionData.Events.FirstOrDefault(e => string.Equals(e.Id, "weather_thunder", StringComparison.Ordinal));
            if (match != null)
            {
                Log.Log("  Story match: weather_thunder");
                return match;
            }
        }

        return null;
    }
    private static readonly LogScope Log = new("Race:Event");
}
