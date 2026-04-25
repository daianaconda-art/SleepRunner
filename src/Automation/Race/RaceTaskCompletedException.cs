namespace SleepRunner.Automation.Race;

/// <summary>
/// 用于在识别到“流程自然结束”类界面时，主动结束跑马脚本。
/// </summary>
public sealed class RaceTaskCompletedException : Exception
{
    public RaceTaskCompletedException(string message) : base(message)
    {
    }
}
