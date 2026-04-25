using SleepRunner.Vision;

namespace SleepRunner.Automation.Race;

/// <summary>
/// 跑马选项处理器接口，每种选项类型实现一个 Handler
/// </summary>
public interface IRaceHandler
{
    string Name { get; }
    int Priority { get; }

    /// <summary>
    /// 判断当前帧是否属于本 Handler 处理的场景
    /// 通过 FrameContext 拿 Mat / 调缓存版 OCR
    /// </summary>
    bool CanHandle(FrameContext frame);

    /// <summary>
    /// 生成当前界面的决策说明（仅用于单步日志展示，不执行点击）
    /// </summary>
    string DescribeDecision(FrameContext frame) => Name;

    /// <summary>
    /// 执行处理逻辑
    /// </summary>
    Task HandleAsync(GameContext ctx);

    /// <summary>
    /// 探测模式逻辑：仅用于定位/预览，不执行点击
    /// </summary>
    Task ProbeAsync(GameContext ctx) => Task.CompletedTask;
}
