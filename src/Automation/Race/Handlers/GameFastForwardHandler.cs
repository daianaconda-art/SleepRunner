using SleepRunner.Input;
using SleepRunner.Utils;
using SleepRunner.Vision;

namespace SleepRunner.Automation.Race.Handlers;

public class GameFastForwardHandler : IRaceHandler
{
    private readonly GameFastForwardStartupState _startup = new();
    private GameFastForwardStartupAction _pendingStartupAction;

    public string Name => "\u6e38\u620f\u5feb\u8fdb\u5f00\u5173";
    public int Priority => 1;

    public bool CanHandle(FrameContext frame)
    {
        if (_startup.IsComplete)
            return false;

        var state = GameFastForwardToggle.DetectState(frame.Screenshot, out double grayRatio, out double brightRatio);
        var action = _startup.Observe(state);
        _pendingStartupAction = GameFastForwardStartupAction.None;

        if (action is GameFastForwardStartupAction.Complete)
        {
            Log.Log($"Game fast-forward startup complete: already two-speed(grayRatio={grayRatio:F3}, brightRatio={brightRatio:F3}).");
            return false;
        }

        bool hit = action is GameFastForwardStartupAction.Click or GameFastForwardStartupAction.ClickAndComplete;
        if (!hit)
            return false;

        _pendingStartupAction = action;
        string suffix = action is GameFastForwardStartupAction.ClickAndComplete
            ? " and finish startup after click"
            : "";
        Log.Log($"Game fast-forward startup needs advance(state={state}, grayRatio={grayRatio:F3}, brightRatio={brightRatio:F3}){suffix}.");
        return true;
    }

    public string DescribeDecision(FrameContext frame)
    {
        var state = GameFastForwardToggle.DetectState(frame.Screenshot, out double grayRatio, out double brightRatio);
        return $"Game fast-forward: state={state}, grayRatio={grayRatio:F3}, brightRatio={brightRatio:F3} -> click toward two-speed";
    }

    public async Task HandleAsync(GameContext ctx)
    {
        var action = _pendingStartupAction;
        Log.Log($"Game fast-forward: click toward two-speed at ({GameFastForwardToggle.ClickX:F3},{GameFastForwardToggle.ClickY:F3}).");
        await ctx.ClickAtPercent(GameFastForwardToggle.ClickX, GameFastForwardToggle.ClickY);
        await ctx.Wait(500);
        _startup.MarkActionExecuted(action);
        if (_startup.IsComplete)
            Log.Log("Game fast-forward startup complete after click.");
        _pendingStartupAction = GameFastForwardStartupAction.None;
    }

    public async Task ProbeAsync(GameContext ctx)
    {
        using var shot = ctx.CaptureScreen();
        if (shot == null || shot.Empty())
            return;

        int x = (int)(shot.Width * GameFastForwardToggle.ClickX);
        int y = (int)(shot.Height * GameFastForwardToggle.ClickY);
        Log.Log($"Game fast-forward probe: move point=({GameFastForwardToggle.ClickX:F3},{GameFastForwardToggle.ClickY:F3}) => ({x},{y})");
        await MouseSimulator.MoveToClient(ctx.WindowHandle, x, y);
        await ctx.Wait(200);
    }

    private static readonly LogScope Log = new("Race:GameFastForward");
}
