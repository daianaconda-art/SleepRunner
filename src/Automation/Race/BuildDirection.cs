namespace SleepRunner.Automation.Race;

/// <summary>
/// 跑马基调方向：攻击型 or 生存型。
/// 目前只用于训练策略等非事件逻辑；事件选择以 events profile JSON 为准。
/// </summary>
public enum BuildDirection
{
    Attack,
    Survival
}
