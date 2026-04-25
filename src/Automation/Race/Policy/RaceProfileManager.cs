using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Policy;

/// <summary>
/// 配置 profile 中心：按 events / cards / trade 三个目录扫描 *.json，并追踪当前选中名
///
/// 设计目标：
/// - 用户可以在 assets/{events,cards,trade}/ 目录下放任意多个 JSON（每个文件 = 一种"风格"）
/// - 工具运行时读"当前选中"的那一份；切换 profile 立即热更新（订阅方各自重读）
/// - 默认 profile 名 = "default"，UI 启动时若没有选过则用这个
/// - 持久化由 UserSettings 负责，本类只负责"现在用哪个"+ "目录里有哪些"
/// </summary>
public static class RaceProfileManager
{
    /// <summary>events profile 切换后触发；payload = 新名字</summary>
    public static event Action<string>? EventsProfileChanged;
    /// <summary>cards profile 切换后触发</summary>
    public static event Action<string>? CardsProfileChanged;
    /// <summary>trade profile 切换后触发</summary>
    public static event Action<string>? TradeProfileChanged;

    private static string _eventsProfile = DefaultProfileName;
    private static string _cardsProfile = DefaultProfileName;
    private static string _tradeProfile = DefaultProfileName;

    public const string DefaultProfileName = "default";

    public static string EventsDir => Path.Combine(PathHelper.BaseDir, "assets", "events");
    public static string CardsDir => Path.Combine(PathHelper.BaseDir, "assets", "cards");
    public static string TradeDir => Path.Combine(PathHelper.BaseDir, "assets", "trade");

    /// <summary>当前生效的事件 profile 名（不带 .json 后缀）</summary>
    public static string CurrentEventsProfile => _eventsProfile;
    public static string CurrentCardsProfile => _cardsProfile;
    public static string CurrentTradeProfile => _tradeProfile;

    /// <summary>解析为绝对路径；profile 名不带 .json 后缀</summary>
    public static string ResolveEventsPath(string profile) =>
        Path.Combine(EventsDir, SanitizeProfile(profile) + ".json");
    public static string ResolveCardsPath(string profile) =>
        Path.Combine(CardsDir, SanitizeProfile(profile) + ".json");
    public static string ResolveTradePath(string profile) =>
        Path.Combine(TradeDir, SanitizeProfile(profile) + ".json");

    /// <summary>当前 events JSON 文件的绝对路径</summary>
    public static string CurrentEventsPath => ResolveEventsPath(_eventsProfile);
    public static string CurrentCardsPath => ResolveCardsPath(_cardsProfile);
    public static string CurrentTradePath => ResolveTradePath(_tradeProfile);

    /// <summary>列出 events 目录下所有 *.json（去后缀），按字母排序；目录不存在返回空列表</summary>
    public static IReadOnlyList<string> ListEventsProfiles() => ListProfiles(EventsDir);
    public static IReadOnlyList<string> ListCardsProfiles() => ListProfiles(CardsDir);
    public static IReadOnlyList<string> ListTradeProfiles() => ListProfiles(TradeDir);

    /// <summary>
    /// 切换 events profile；同名跳过；新名不在目录列表中也允许（用户可能预先在 settings 写了，文件未到位）
    /// </summary>
    public static void SetEventsProfile(string name)
    {
        string cleaned = SanitizeProfile(name);
        if (string.Equals(_eventsProfile, cleaned, StringComparison.Ordinal)) return;
        _eventsProfile = cleaned;
        Logger.Log($"[RaceProfileManager] Events profile -> '{cleaned}' (path={CurrentEventsPath})");
        SafeRaise(EventsProfileChanged, cleaned);
    }

    public static void SetCardsProfile(string name)
    {
        string cleaned = SanitizeProfile(name);
        if (string.Equals(_cardsProfile, cleaned, StringComparison.Ordinal)) return;
        _cardsProfile = cleaned;
        Logger.Log($"[RaceProfileManager] Cards profile -> '{cleaned}' (path={CurrentCardsPath})");
        SafeRaise(CardsProfileChanged, cleaned);
    }

    public static void SetTradeProfile(string name)
    {
        string cleaned = SanitizeProfile(name);
        if (string.Equals(_tradeProfile, cleaned, StringComparison.Ordinal)) return;
        _tradeProfile = cleaned;
        Logger.Log($"[RaceProfileManager] Trade profile -> '{cleaned}' (path={CurrentTradePath})");
        SafeRaise(TradeProfileChanged, cleaned);
    }

    private static IReadOnlyList<string> ListProfiles(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return Array.Empty<string>();
            return Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Logger.Log($"[RaceProfileManager] List profiles failed at '{dir}': {ex.Message}");
            return Array.Empty<string>();
        }
    }

    /// <summary>过滤路径分隔符与多余空白；空字符串回退为 default</summary>
    private static string SanitizeProfile(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return DefaultProfileName;
        string s = raw.Trim();
        foreach (var ch in Path.GetInvalidFileNameChars())
            s = s.Replace(ch.ToString(), "");
        if (s.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            s = s.Substring(0, s.Length - 5);
        return string.IsNullOrEmpty(s) ? DefaultProfileName : s;
    }

    private static void SafeRaise(Action<string>? evt, string payload)
    {
        if (evt == null) return;
        try { evt.Invoke(payload); }
        catch (Exception ex) { Logger.Log($"[RaceProfileManager] Subscriber threw: {ex.Message}"); }
    }
}
