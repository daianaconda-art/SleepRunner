using SleepRunner.Automation.Race.Policy.Training;

namespace SleepRunner.Forms;

internal static class UiText
{
    internal static class App
    {
        public const string WindowTitle = "跑马助手";
        public const string FooterHint = "按住标题栏可拖动  •  Esc 关闭  •  拖动边缘可调整大小";
        public const string Version = "版本 1.0";
    }

    internal static class Sections
    {
        public const string Tuning = "调校";
        public const string Profiles = "配置方案";
        public const string TrainingRules = "训练规则";
        public const string ConfigDirs = "配置目录";
    }

    internal static class Actions
    {
        public const string Start = "开始";
        public const string Stop = "停止";
        public const string Edit = "编辑";
        public const string Duplicate = "复制";
        public const string OpenDirectory = "打开目录";
        public const string AddRule = "新增规则";
        public const string Save = "保存";
        public const string Cancel = "取消";
        public const string MoveUp = "上移";
        public const string MoveDown = "下移";
        public const string Delete = "删除";
        public const string OpenEditor = "打开编辑器";
    }

    internal static class Status
    {
        public const string IdleTitle = "空闲";
        public const string IdleSubtitle = "进入跑马界面后点击开始";
        public const string StoppedTitle = "已停止";
        public const string StoppedSubtitle = "上次运行已结束";
        public const string RunningTitle = "运行中";
        public const string RunningSubtitle = "正在自动跑马";
        public const string PausedTitle = "已暂停";
        public const string PausedSubtitle = "等待继续";
        public const string StoppingTitle = "正在停止";
        public const string StoppingSubtitle = "正在清理...";
    }

    internal static class Config
    {
        public const string FailRateTitle = "失败率上限";
        public const string FailRateHint = "超过该失败率时跳过训练";
        public const string WaitMultiplierTitle = "等待倍率";
        public const string WaitMultiplierHint = "统一放慢所有等待时间";
        public const string ClickSpeedTitle = "点击速度";
        public const string ClickSpeedHint = "调整拟人点击间隔";
        public const string RushThresholdTitle = "猛攻阈值";
        public const string RushThresholdHint = "攻击=力量阈值，生存=体力阈值";
        public const string BuildDirectionTitle = "养成方向";
        public const string BuildDirectionHint = "本轮跑马的策略基调";
        public static readonly string[] BuildSegments = ["攻击", "生存"];
        public const string AppraiseDifficultyTitle = "评鉴战";
        public const string AppraiseDifficultyHint = "普通选第2项，困难选第3项";
        public static readonly string[] AppraiseDifficultySegments = ["普通", "困难"];
    }

    internal static class Profiles
    {
        public const string Events = "事件";
        public const string Cards = "选卡";
        public const string Trade = "交易";
    }

    internal static class Files
    {
        public const string EventsDir = "事件目录";
        public const string CardsDir = "选卡目录";
        public const string TradeDir = "交易目录";
        public const string EventsTooltip = "打开 assets/events/";
        public const string CardsTooltip = "打开 assets/cards/";
        public const string TradeTooltip = "打开 assets/trade/";
    }

    internal static class Training
    {
        public const string PanelProfileLabel = "配置";
        public const string ProfileNameLabel = "配置名";
        public const string DialogTitle = "训练规则";
        public const string EmptyProfileNameMessage = "请输入配置名后再继续。";
        public const string DuplicateProfilePromptTitle = "复制训练配置";
        public const string RuleId = "规则编号";
        public const string Enabled = "启用";
        public const string Condition = "条件";
        public const string Action = "动作";
        public const string Fallback = "兜底";
        public const string EditorHint = "每张规则卡只编辑一个条件和一个动作。兜底卡会固定在最后，并且只编辑动作。";
        public const string SaveErrorTitle = "无法保存训练规则";
        public const string BuiltinDefault = "内置默认";

        public static string ProfileCaption(string profileName) => $"配置：{profileName}";

        public static string EditTitle(string profileName) => $"编辑训练规则 - {profileName}";

        public static string DuplicateTitle(string profileName) => $"复制训练规则 - {profileName}";

        public static string OverwriteMessage(string profileName) => $"“{profileName}” 已存在，要覆盖吗？";

        public static string FieldLabel(TrainingRuleField field) => field switch
        {
            TrainingRuleField.StrengthIcons => "力量图标",
            TrainingRuleField.StaminaIcons => "体力图标",
            TrainingRuleField.AgilityIcons => "韧性图标",
            TrainingRuleField.FocusIcons => "集中图标",
            TrainingRuleField.GuardIcons => "保护图标",
            TrainingRuleField.StrengthFailRate => "力量失败率",
            TrainingRuleField.StaminaFailRate => "体力失败率",
            TrainingRuleField.AgilityFailRate => "韧性失败率",
            TrainingRuleField.FocusFailRate => "集中失败率",
            TrainingRuleField.GuardFailRate => "保护失败率",
            TrainingRuleField.AnyFailRate => "任意失败率",
            TrainingRuleField.StrengthStat => "力量属性",
            TrainingRuleField.StaminaStat => "体力属性",
            _ => field.ToString(),
        };

        public static string ActionLabel(TrainingDecisionAction action) => action switch
        {
            TrainingDecisionAction.TrainStrength => "训练力量",
            TrainingDecisionAction.TrainStamina => "训练体力",
            TrainingDecisionAction.TrainAgility => "训练韧性",
            TrainingDecisionAction.TrainFocus => "训练集中",
            TrainingDecisionAction.TrainGuard => "训练保护",
            TrainingDecisionAction.Rest => "休息",
            TrainingDecisionAction.BuiltinDefault => BuiltinDefault,
            _ => action.ToString(),
        };
    }
}
