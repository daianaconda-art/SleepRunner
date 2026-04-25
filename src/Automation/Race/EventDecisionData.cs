using System.Text.Json.Serialization;

namespace SleepRunner.Automation.Race;

/// <summary>
/// 跑马基调方向：攻击型 or 生存型
/// 仅作为"内置硬编码逻辑"（PowerRush / Stamina rush / 训练破平局 / Trade strength offers）的开关；
/// 事件 / 选卡 / 交易关键词决策已挪到 events/ + cards/ + trade/ 三套 profile JSON 自行配置。
/// </summary>
public enum BuildDirection
{
    Attack,
    Survival
}

/// <summary>
/// 事件决策 JSON 顶层结构：events 数组按顺序匹配
/// </summary>
public class EventDecisionData
{
    [JsonPropertyName("events")]
    public List<RaceEvent> Events { get; set; } = [];
}

public class RaceEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("event_name")]
    public string EventName { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    /// <summary>
    /// confirmed=有明确推荐, pending=需人工确认。
    /// 历史 build_dependent 已废弃：用户通过维护 events/attack.json + events/survival.json 两份独立 profile 实现差异化。
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("recommended_option")]
    public int? RecommendedOption { get; set; }

    [JsonPropertyName("fallback_option")]
    public int? FallbackOption { get; set; }

    [JsonPropertyName("options")]
    public List<EventOption> Options { get; set; } = [];

    /// <summary>
    /// 单事件 Y 坐标覆盖：按选项 index 1..N 顺序的 Y 百分比；元素为 null 表示该项仍走默认布局。
    /// 用于"选项不在底部对齐"的特殊事件（如右下角气泡、贴角色侧弹出框等）。
    /// </summary>
    [JsonPropertyName("option_y_overrides")]
    public List<double?>? OptionYOverrides { get; set; }

    [JsonPropertyName("note")]
    public string Note { get; set; } = "";

    /// <summary>
    /// 直接返回 RecommendedOption；不再按基调路由（基调差异由用户切 profile 实现）
    /// </summary>
    public int? GetRecommendedOption() => RecommendedOption;
}

public class EventOption
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("keyword")]
    public string Keyword { get; set; } = "";

    [JsonPropertyName("alias")]
    public List<string> Alias { get; set; } = [];
}
