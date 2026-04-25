using SleepRunner.Utils;

namespace SleepRunner.Automation.Race;

/// <summary>
/// 跑马自动化运行时可调配置中心
/// 所有配置都是 static，UI/CLI 任意线程随时可改，handler 每帧读取实现热更新
/// 注意：set 仅做边界 clamp，不做线程同步（int 读写本身原子，足够当前场景）
/// </summary>
public static class RaceConfig
{
    /// <summary>任意配置变化时触发；UI 可订阅此事件做节流持久化。</summary>
    public static event Action? Changed;

    private static int _failRateThreshold = 30;

    /// <summary>
    /// 训练失败率阈值（百分比）：扫描某行训练若失败率 > 该值则触发休息
    /// 默认 30%。允许范围 [0, 100]。
    /// </summary>
    public static int FailRateThreshold
    {
        get => _failRateThreshold;
        set
        {
            int clamped = Math.Clamp(value, 0, 100);
            if (clamped == _failRateThreshold)
                return;
            int previous = _failRateThreshold;
            _failRateThreshold = clamped;
            Logger.Log($"[RaceConfig] FailRateThreshold changed: {previous}% -> {clamped}%");
            RaiseChanged();
        }
    }

    private static double _waitMultiplier = 1.0;

    /// <summary>
    /// 全局等待倍率：GameContext.Wait 内部用 (ms * 倍率) 实现整体加速/减速。
    /// 默认 1.0；范围 [0.5, 2.0]；&lt;1.0 加速但可能导致动画未结束就截屏失败。
    /// 注意：double 在 32 位平台读写非原子，但当前进程跑 x64 + 容忍偶发不一致，足够。
    /// </summary>
    public static double WaitMultiplier
    {
        get => _waitMultiplier;
        set
        {
            double clamped = Math.Clamp(value, 0.5, 2.0);
            if (Math.Abs(clamped - _waitMultiplier) < 0.001)
                return;
            double previous = _waitMultiplier;
            _waitMultiplier = clamped;
            Logger.Log($"[RaceConfig] WaitMultiplier changed: {previous:F2}x -> {clamped:F2}x");
            RaiseChanged();
        }
    }

    private static double _clickSpeedMultiplier = 1.0;

    /// <summary>
    /// 点击拟人延迟倍率：作用在 MouseSimulator 内所有 RandDelay
    /// （置顶等待 / 鼠标曲线步长 / 按下时长 等）
    /// 默认 1.0；范围 [0.3, 2.0]；&lt;1.0 点击更快但更"机器"，&gt;1.0 更像真人但慢
    /// </summary>
    public static double ClickSpeedMultiplier
    {
        get => _clickSpeedMultiplier;
        set
        {
            double clamped = Math.Clamp(value, 0.3, 2.0);
            if (Math.Abs(clamped - _clickSpeedMultiplier) < 0.001)
                return;
            double previous = _clickSpeedMultiplier;
            _clickSpeedMultiplier = clamped;
            Logger.Log($"[RaceConfig] ClickSpeedMultiplier changed: {previous:F2}x -> {clamped:F2}x");
            RaiseChanged();
        }
    }

    private static int _powerRushThreshold = 450;

    /// <summary>
    /// 力量猛攻触发阈值：Attack 基调下，力量值 &gt; 该值时训练页直接选力量训练（fast-path）。
    /// 默认 450。允许范围 [100, 1200]；上限设 1200 是为了与 PowerMaxValue=1250 留出 50 的缓冲。
    /// 调低会更早进入猛攻、跑完力量更稳；调高会让前期保留更多体力/韧性练习机会。
    /// </summary>
    public static int PowerRushThreshold
    {
        get => _powerRushThreshold;
        set
        {
            int clamped = Math.Clamp(value, 100, 1200);
            if (clamped == _powerRushThreshold)
                return;
            int previous = _powerRushThreshold;
            _powerRushThreshold = clamped;
            Logger.Log($"[RaceConfig] PowerRushThreshold changed: {previous} -> {clamped}");
            RaiseChanged();
        }
    }

    private static BuildDirection _buildDirection = BuildDirection.Attack;

    /// <summary>
    /// 跑马基调方向：影响 build_dependent 事件、训练优先级、交易购买偏好等。
    /// 由 UI 选择并随 UserSettings 持久化；变更只对下一次 Start 生效。
    /// </summary>
    public static BuildDirection BuildDirection
    {
        get => _buildDirection;
        set
        {
            if (value == _buildDirection)
                return;
            var previous = _buildDirection;
            _buildDirection = value;
            Logger.Log($"[RaceConfig] BuildDirection changed: {previous} -> {value}");
            RaiseChanged();
        }
    }

    private static void RaiseChanged()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Log($"[RaceConfig] Changed handler threw: {ex.Message}");
        }
    }
}
