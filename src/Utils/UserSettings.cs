using System.Text.Json;
using System.Text.Json.Serialization;
using SleepRunner.Automation.Race;
using SleepRunner.Automation.Race.Policy;
using SleepRunner.Automation.Race.Policy.Training;

namespace SleepRunner.Utils;

/// <summary>
/// 用户偏好设置：UI 与运行参数的本地持久化层
/// 设计目标：
///   - 让脚本下次启动时直接复用上次的失败率阈值、等待倍率、基调、窗口位置等
///   - 保持极简：JSON 文件 + 一次 Load + 节流 Save，足以覆盖单机使用场景
///   - 与 RaceConfig 解耦：RaceConfig 仍然是运行时唯一真值，UserSettings 只负责持久化
/// </summary>
public sealed class UserSettings
{
    /// <summary>训练失败率阈值（%）</summary>
    [JsonPropertyName("fail_rate_threshold")]
    public int FailRateThreshold { get; set; } = 30;

    /// <summary>全局等待倍率</summary>
    [JsonPropertyName("wait_multiplier")]
    public double WaitMultiplier { get; set; } = 1.0;

    /// <summary>点击拟人延迟倍率（&lt;1.0 更快、&gt;1.0 更慢）</summary>
    [JsonPropertyName("click_speed_multiplier")]
    public double ClickSpeedMultiplier { get; set; } = 1.0;

    /// <summary>跑马基调方向（Attack / Survival）</summary>
    [JsonPropertyName("build_direction")]
    public BuildDirection BuildDirection { get; set; } = BuildDirection.Attack;

    /// <summary>评鉴战难度：Normal 选第二项，Hard 选第三项；默认沿用困难。</summary>
    [JsonPropertyName("appraise_difficulty_mode")]
    public AppraiseDifficultyMode AppraiseDifficultyMode { get; set; } = AppraiseDifficultyMode.Hard;

    /// <summary>力量猛攻触发阈值；范围 [100,1200]；默认 450</summary>
    [JsonPropertyName("power_rush_threshold")]
    public int PowerRushThreshold { get; set; } = 450;

    /// <summary>主窗口左上角 X，<0 表示未设置</summary>
    [JsonPropertyName("window_x")]
    public int WindowX { get; set; } = -1;

    /// <summary>主窗口左上角 Y，<0 表示未设置</summary>
    [JsonPropertyName("window_y")]
    public int WindowY { get; set; } = -1;

    /// <summary>主窗口宽度（client），<=0 表示用默认值</summary>
    [JsonPropertyName("window_w")]
    public int WindowWidth { get; set; } = 0;

    /// <summary>主窗口高度（client），<=0 表示用默认值</summary>
    [JsonPropertyName("window_h")]
    public int WindowHeight { get; set; } = 0;

    /// <summary>是否窗口置顶，方便覆盖在游戏窗口上</summary>
    [JsonPropertyName("top_most")]
    public bool TopMost { get; set; } = true;

    /// <summary>当前生效的事件 profile 名（assets/events/&lt;name&gt;.json，不带后缀）</summary>
    [JsonPropertyName("events_profile")]
    public string EventsProfile { get; set; } = RaceProfileManager.DefaultProfileName;

    /// <summary>当前生效的卡片选择 profile 名（assets/cards/&lt;name&gt;.json）</summary>
    [JsonPropertyName("cards_profile")]
    public string CardsProfile { get; set; } = RaceProfileManager.DefaultProfileName;

    /// <summary>当前生效的交易 profile 名（assets/trade/&lt;name&gt;.json）</summary>
    [JsonPropertyName("trade_profile")]
    public string TradeProfile { get; set; } = RaceProfileManager.DefaultProfileName;

    [JsonPropertyName("training_profile")]
    public string TrainingProfile { get; set; } = TrainingRuleProfileManager.DefaultProfileName;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() },
    };

    private static string SettingsPath => Path.Combine(PathHelper.Resolve("assets/config"), "user_settings.json");

    /// <summary>
    /// 从磁盘加载设置；文件不存在或解析失败时返回默认值（不抛异常）
    /// </summary>
    public static UserSettings Load()
    {
        try
        {
            string path = SettingsPath;
            if (!File.Exists(path))
            {
                Logger.Log($"[Settings] No settings file at {path}, using defaults.");
                return new UserSettings();
            }

            string json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<UserSettings>(json, JsonOpts);
            if (loaded == null)
            {
                Logger.Log("[Settings] Settings file empty/invalid, using defaults.");
                return new UserSettings();
            }

            loaded.Clamp();
            Logger.Log($"[Settings] Loaded: failRate={loaded.FailRateThreshold}%, " +
                       $"waitX={loaded.WaitMultiplier:F2}, clickX={loaded.ClickSpeedMultiplier:F2}, build={loaded.BuildDirection}, " +
                       $"appraise={loaded.AppraiseDifficultyMode}, powerRush={loaded.PowerRushThreshold}, " +
                       $"profiles=(events={loaded.EventsProfile}, cards={loaded.CardsProfile}, trade={loaded.TradeProfile}, training={loaded.TrainingProfile}), " +
                       $"window=({loaded.WindowX},{loaded.WindowY},{loaded.WindowWidth}x{loaded.WindowHeight}), " +
                       $"topMost={loaded.TopMost}");
            return loaded;
        }
        catch (Exception ex)
        {
            Logger.Log($"[Settings] Load failed: {ex.Message}, using defaults.");
            return new UserSettings();
        }
    }

    /// <summary>
    /// 保存当前设置到磁盘；失败时只记录日志，不抛异常
    /// </summary>
    public void Save()
    {
        try
        {
            Clamp();
            string path = SettingsPath;
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(this, JsonOpts);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Logger.Log($"[Settings] Save failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 把当前设置写回 RaceConfig，作为运行时真值
    /// </summary>
    public void ApplyToRaceConfig()
    {
        RaceConfig.FailRateThreshold = FailRateThreshold;
        RaceConfig.WaitMultiplier = WaitMultiplier;
        RaceConfig.ClickSpeedMultiplier = ClickSpeedMultiplier;
        RaceConfig.BuildDirection = BuildDirection;
        RaceConfig.AppraiseDifficultyMode = AppraiseDifficultyMode;
        RaceConfig.PowerRushThreshold = PowerRushThreshold;

        // profile 选择是全局状态：UI 读 RaceProfileManager，决策侧也读 RaceProfileManager；
        // 这里把 settings 推进去，后续切换由 UI 调 SetXxxProfile 同步回 settings 即可。
        RaceProfileManager.SetEventsProfile(EventsProfile);
        RaceProfileManager.SetCardsProfile(CardsProfile);
        RaceProfileManager.SetTradeProfile(TradeProfile);
        TrainingRuleProfileManager.SetCurrentProfile(TrainingProfile);
    }

    /// <summary>
    /// 边界 clamp，确保读到非法值时不会污染 RaceConfig
    /// </summary>
    private void Clamp()
    {
        FailRateThreshold = Math.Clamp(FailRateThreshold, 0, 100);
        if (WaitMultiplier < 0.5 || WaitMultiplier > 2.0)
            WaitMultiplier = Math.Clamp(WaitMultiplier, 0.5, 2.0);
        if (ClickSpeedMultiplier < 0.3 || ClickSpeedMultiplier > 2.0)
            ClickSpeedMultiplier = Math.Clamp(ClickSpeedMultiplier, 0.3, 2.0);
        if (!Enum.IsDefined(AppraiseDifficultyMode))
            AppraiseDifficultyMode = AppraiseDifficultyMode.Hard;
        PowerRushThreshold = Math.Clamp(PowerRushThreshold, 100, 1200);
        if (WindowWidth < 0) WindowWidth = 0;
        if (WindowHeight < 0) WindowHeight = 0;

        if (string.IsNullOrWhiteSpace(EventsProfile)) EventsProfile = RaceProfileManager.DefaultProfileName;
        if (string.IsNullOrWhiteSpace(CardsProfile)) CardsProfile = RaceProfileManager.DefaultProfileName;
        if (string.IsNullOrWhiteSpace(TradeProfile)) TradeProfile = RaceProfileManager.DefaultProfileName;
        if (string.IsNullOrWhiteSpace(TrainingProfile)) TrainingProfile = TrainingRuleProfileManager.DefaultProfileName;
    }
}
