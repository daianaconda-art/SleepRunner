using SleepRunner.Automation;
using SleepRunner.Automation.Race;

namespace SleepRunner.Forms;


/// <summary>
/// UI 与跑马后台之间的契约：UI 不直接依赖 Automation/* 实现
/// </summary>
public interface IRaceController : IDisposable
{
    /// <summary>当前生命周期状态</summary>
    RaceState State { get; }

    /// <summary>状态变化事件（可能在后台线程触发；UI 订阅时需 BeginInvoke 回主线程）</summary>
    event Action<RaceState>? StateChanged;

    /// <summary>
    /// 实时活动描述变化（"决策中：训练" / "执行：商人" / "扫描画面" 之类的简短中文短语）
    /// 触发可能在后台线程，UI 订阅时需切回主线程
    /// </summary>
    event Action<string>? ActivityChanged;

    /// <summary>启动跑马；状态非 Idle/Stopped 时直接忽略</summary>
    void Start();

    /// <summary>暂停：当前 handler 跑完后停在下一次 dispatch 前</summary>
    void Pause();

    /// <summary>从暂停恢复</summary>
    void Resume();

    /// <summary>停止：取消令牌触发，等待 race task 退出</summary>
    Task StopAsync();
}
