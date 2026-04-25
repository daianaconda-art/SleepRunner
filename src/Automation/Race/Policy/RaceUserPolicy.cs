using System.Text.Json;
using System.Text.Json.Serialization;
using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Policy;

/// <summary>
/// 用户可编辑策略中心：当前选中的 cards profile + trade profile 的关键词
///
/// 设计目标（已重构为 profile 模式）：
/// - 卡片 / 交易策略各自由 RaceProfileManager 选择目录下哪一个 *.json
/// - 文件 mtime 变化或 profile 切换都会触发下一次访问时重读
/// - 解析失败 / 文件缺失时退回内置默认值，保持现有行为不变
/// - 节流：mtime 检查最快 2 秒一次，避免每帧 stat 影响性能
/// - schema 已极简："priority_keywords + whitelist" / "keywords"，没有基调二段，没有 "+" 语法
/// </summary>
public static class RaceUserPolicy
{
    /// <summary>策略发生热重载后触发；UI 可订阅做提示，无订阅也无副作用。</summary>
    public static event Action? Reloaded;

    private static readonly object _lock = new();
    private static readonly TimeSpan MtimeCheckInterval = TimeSpan.FromSeconds(2);

    private static DateTime _lastCardCheckUtc = DateTime.MinValue;
    private static DateTime _lastTradeCheckUtc = DateTime.MinValue;
    private static DateTime _cardFileMtimeUtc = DateTime.MinValue;
    private static DateTime _tradeFileMtimeUtc = DateTime.MinValue;
    private static string _cardLoadedPath = string.Empty;
    private static string _tradeLoadedPath = string.Empty;
    private static bool _cardLoaded;
    private static bool _tradeLoaded;

    private static List<PriorityKeyword> _cardPriorityOrder = DefaultCardPriority();
    private static List<CardWhitelistRule> _cardWhitelist = [];
    private static List<string> _tradeKeywords = DefaultTradeKeywords();
    private static bool _tradePreferStrengthItems = true;

    static RaceUserPolicy()
    {
        // profile 切换 → 强制下一次访问重读
        RaceProfileManager.CardsProfileChanged += _ => ForceReloadCards();
        RaceProfileManager.TradeProfileChanged += _ => ForceReloadTrade();
    }

    /// <summary>当前 cards profile 对应的 JSON 路径（UI 快捷入口用）</summary>
    public static string CurrentCardsPath => RaceProfileManager.CurrentCardsPath;
    /// <summary>当前 trade profile 对应的 JSON 路径</summary>
    public static string CurrentTradePath => RaceProfileManager.CurrentTradePath;

    /// <summary>当前 cards profile 的优先级表（数组顺序即优先级，靠前优先）</summary>
    public static IReadOnlyList<PriorityKeyword> CardPriorityOrder
    {
        get { EnsureCardLoaded(); return _cardPriorityOrder; }
    }

    /// <summary>整张卡片"特殊白名单"，命中后才走优先级匹配</summary>
    public static IReadOnlyList<CardWhitelistRule> CardWhitelist
    {
        get { EnsureCardLoaded(); return _cardWhitelist; }
    }

    /// <summary>当前 trade profile 的购买关键词列表（命中商品名或效果文本任一关键词即买）</summary>
    public static IReadOnlyList<string> TradeKeywords
    {
        get { EnsureTradeLoaded(); return _tradeKeywords; }
    }

    /// <summary>当前 trade profile 是否额外把力量增益商品加入购买队列。</summary>
    public static bool TradePreferStrengthItems
    {
        get { EnsureTradeLoaded(); return _tradePreferStrengthItems; }
    }

    /// <summary>
    /// 强制下一次访问重新解析；profile 切换 / 外部要求 reload 时调用
    /// </summary>
    public static void ForceReload()
    {
        ForceReloadCards();
        ForceReloadTrade();
        EnsureCardLoaded();
        EnsureTradeLoaded();
        Logger.Log("[RaceUserPolicy] Force reload requested.");
    }

    private static void ForceReloadCards()
    {
        lock (_lock)
        {
            _cardLoaded = false;
            _cardFileMtimeUtc = DateTime.MinValue;
            _lastCardCheckUtc = DateTime.MinValue;
            _cardLoadedPath = string.Empty;
        }
    }

    private static void ForceReloadTrade()
    {
        lock (_lock)
        {
            _tradeLoaded = false;
            _tradeFileMtimeUtc = DateTime.MinValue;
            _lastTradeCheckUtc = DateTime.MinValue;
            _tradeLoadedPath = string.Empty;
        }
    }

    /// <summary>
    /// 在 OCR 文本里查找当前优先级表中匹配的最靠前条目；未命中返回 -1
    /// </summary>
    public static int ResolvePriorityRank(string normalizedText)
    {
        if (string.IsNullOrEmpty(normalizedText))
            return -1;

        var order = CardPriorityOrder;
        for (int i = 0; i < order.Count; i++)
        {
            foreach (var kw in order[i].Keywords)
            {
                if (!string.IsNullOrEmpty(kw) && normalizedText.Contains(kw, StringComparison.Ordinal))
                    return i;
            }
        }
        return -1;
    }

    private static void EnsureCardLoaded()
    {
        bool changed = false;
        lock (_lock)
        {
            string path = RaceProfileManager.CurrentCardsPath;
            DateTime now = DateTime.UtcNow;

            // profile 切换会让 _cardLoadedPath 与新路径不一致 → 立即重读
            bool pathChanged = !string.Equals(_cardLoadedPath, path, StringComparison.OrdinalIgnoreCase);
            if (_cardLoaded && !pathChanged && now - _lastCardCheckUtc < MtimeCheckInterval)
                return;
            _lastCardCheckUtc = now;

            DateTime mtime = SafeGetMtime(path);
            if (_cardLoaded && !pathChanged && mtime == _cardFileMtimeUtc)
                return;

            _cardLoaded = true;
            _cardFileMtimeUtc = mtime;
            _cardLoadedPath = path;

            if (!File.Exists(path))
            {
                _cardPriorityOrder = DefaultCardPriority();
                _cardWhitelist = [];
                Logger.Log($"[RaceUserPolicy] Cards profile not found, using defaults: {path}");
                changed = true;
            }
            else
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var cfg = JsonSerializer.Deserialize<CardPolicyFile>(json) ?? new CardPolicyFile();
                    _cardPriorityOrder = NormalizeOrder(cfg.PriorityKeywords) ?? DefaultCardPriority();
                    _cardWhitelist = cfg.Whitelist ?? [];
                    Logger.Log($"[RaceUserPolicy] Cards profile loaded ({Path.GetFileName(path)}): priority={_cardPriorityOrder.Count}, whitelist={_cardWhitelist.Count}");
                    changed = true;
                }
                catch (Exception ex)
                {
                    _cardPriorityOrder = DefaultCardPriority();
                    _cardWhitelist = [];
                    Logger.Log($"[RaceUserPolicy] Cards profile parse failed ({path}): {ex.Message}, using defaults");
                    changed = true;
                }
            }
        }

        if (changed)
            RaiseReloaded();
    }

    private static void EnsureTradeLoaded()
    {
        bool changed = false;
        lock (_lock)
        {
            string path = RaceProfileManager.CurrentTradePath;
            DateTime now = DateTime.UtcNow;

            bool pathChanged = !string.Equals(_tradeLoadedPath, path, StringComparison.OrdinalIgnoreCase);
            if (_tradeLoaded && !pathChanged && now - _lastTradeCheckUtc < MtimeCheckInterval)
                return;
            _lastTradeCheckUtc = now;

            DateTime mtime = SafeGetMtime(path);
            if (_tradeLoaded && !pathChanged && mtime == _tradeFileMtimeUtc)
                return;

            _tradeLoaded = true;
            _tradeFileMtimeUtc = mtime;
            _tradeLoadedPath = path;

            if (!File.Exists(path))
            {
                _tradeKeywords = DefaultTradeKeywords();
                _tradePreferStrengthItems = DefaultTradePreferStrengthItems();
                Logger.Log($"[RaceUserPolicy] Trade profile not found, using defaults: {path}");
                changed = true;
            }
            else
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var cfg = JsonSerializer.Deserialize<TradePolicyFile>(json) ?? new TradePolicyFile();
                    _tradeKeywords = NormalizeKeywordList(cfg.Keywords) ?? DefaultTradeKeywords();
                    _tradePreferStrengthItems = cfg.PreferStrengthItems ?? DefaultTradePreferStrengthItems();
                    Logger.Log($"[RaceUserPolicy] Trade profile loaded ({Path.GetFileName(path)}): keywords={_tradeKeywords.Count}");
                    changed = true;
                }
                catch (Exception ex)
                {
                    _tradeKeywords = DefaultTradeKeywords();
                    _tradePreferStrengthItems = DefaultTradePreferStrengthItems();
                    Logger.Log($"[RaceUserPolicy] Trade profile parse failed ({path}): {ex.Message}, using defaults");
                    changed = true;
                }
            }
        }

        if (changed)
            RaiseReloaded();
    }

    private static DateTime SafeGetMtime(string path)
    {
        try
        {
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static List<PriorityKeyword>? NormalizeOrder(List<PriorityKeyword>? raw)
    {
        if (raw == null) return null;
        var list = new List<PriorityKeyword>(raw.Count);
        foreach (var entry in raw)
        {
            if (entry == null) continue;
            var kws = entry.Keywords?.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).ToList() ?? [];
            if (kws.Count == 0) continue;
            list.Add(new PriorityKeyword
            {
                Label = string.IsNullOrWhiteSpace(entry.Label) ? kws[0] : entry.Label.Trim(),
                Keywords = kws,
            });
        }
        return list.Count == 0 ? null : list;
    }

    private static List<string>? NormalizeKeywordList(List<string>? raw)
    {
        if (raw == null) return null;
        var list = raw.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
        return list.Count == 0 ? null : list;
    }

    private static List<PriorityKeyword> DefaultCardPriority() =>
    [
        new() { Label = "暴击率", Keywords = ["暴击率", "暴率"] },
        new() { Label = "攻击力", Keywords = ["攻击力", "攻击"] },
        new() { Label = "暴击伤害", Keywords = ["暴击伤害", "爆伤"] },
    ];

    private static List<string> DefaultTradeKeywords() =>
    [
        "抽奖券",
        "耐力",
        "潜质点数",
        "甜甜圈",
    ];

    private static bool DefaultTradePreferStrengthItems() => true;

    private static void RaiseReloaded()
    {
        try
        {
            Reloaded?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Log($"[RaceUserPolicy] Reloaded handler threw: {ex.Message}");
        }
    }

    /// <summary>卡片词条优先级一项：label 仅用于日志/UI 展示，匹配靠 keywords</summary>
    public sealed class PriorityKeyword
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = "";

        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; } = [];
    }

    /// <summary>整张卡的特殊白名单规则；命中 title/card 任一关键词后才进入优先级判定</summary>
    public sealed class CardWhitelistRule
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("title_keywords")]
        public List<string> TitleKeywords { get; set; } = [];

        [JsonPropertyName("card_keywords")]
        public List<string> CardKeywords { get; set; } = [];
    }

    private sealed class CardPolicyFile
    {
        [JsonPropertyName("priority_keywords")]
        public List<PriorityKeyword>? PriorityKeywords { get; set; }

        [JsonPropertyName("whitelist")]
        public List<CardWhitelistRule>? Whitelist { get; set; }
    }

    private sealed class TradePolicyFile
    {
        [JsonPropertyName("keywords")]
        public List<string>? Keywords { get; set; }

        [JsonPropertyName("prefer_strength_items")]
        public bool? PreferStrengthItems { get; set; }
    }
}
