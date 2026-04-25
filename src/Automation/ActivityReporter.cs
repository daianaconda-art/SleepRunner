namespace SleepRunner.Automation;

/// <summary>
/// 自动化"活动描述"全局广播：让 UI 能实时看到当前在干嘛
///
/// 使用方式：
///   ActivityReporter.Set("决策中：训练");
///
/// 设计取向：
///   - 无依赖、无锁、纯静态，任何 Handler / Runner 都能直接调用
///   - 是 UI 提示性质，不参与业务逻辑；所以即便 UI 没订阅也不影响后台
///   - 节流：相邻同样的描述会被忽略，避免主循环每帧重复刷新
/// </summary>
public static class ActivityReporter
{
    private static string _current = "";
    private static readonly object _lock = new();

    /// <summary>当前活动描述变化时触发（在调用 Set 的线程上同步触发）</summary>
    public static event Action<string>? Changed;

    /// <summary>当前活动描述</summary>
    public static string Current
    {
        get { lock (_lock) return _current; }
    }

    /// <summary>
    /// 设置当前活动描述；与上次相同时跳过广播，避免高频刷新
    /// 传入 null / 空串等价于 Clear
    /// </summary>
    public static void Set(string? text)
    {
        string value = text ?? string.Empty;
        lock (_lock)
        {
            if (_current == value) return;
            _current = value;
        }
        try { Changed?.Invoke(value); }
        catch { /* 上层订阅崩了不能影响自动化主循环 */ }
    }

    /// <summary>清空活动描述（一般在 race session 结束时调用）</summary>
    public static void Clear() => Set(string.Empty);
}
