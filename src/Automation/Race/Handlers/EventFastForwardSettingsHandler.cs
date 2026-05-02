using SleepRunner.Automation.Race.Handlers.Events;
using SleepRunner.Input;
using SleepRunner.Utils;
using SleepRunner.Vision;

namespace SleepRunner.Automation.Race.Handlers;

public class EventFastForwardSettingsHandler : IRaceHandler
{
    private const double FastForwardAllClickX = 0.695;
    private const double FastForwardAllClickY = 0.485;
    private const double ConfirmClickX = 0.500;
    private const double ConfirmClickY = 0.745;

    public string Name => "\u4e8b\u4ef6\u5feb\u8f6c\u8bbe\u5b9a";
    public int Priority => 3;

    public bool CanHandle(FrameContext frame)
    {
        bool hit = EventFastForwardSettingsScreenChecks.IsScreen(frame.Screenshot, out string summary);
        if (hit)
            Log.Log($"Event fast-forward settings hit: {summary}");
        return hit;
    }

    public string DescribeDecision(FrameContext frame)
    {
        EventFastForwardSettingsScreenChecks.IsScreen(frame.Screenshot, out string summary);
        return $"Event fast-forward settings: click all-events card then confirm ({summary})";
    }

    public async Task HandleAsync(GameContext ctx)
    {
        Log.Log(
            $"Event fast-forward settings: click all-events card at ({FastForwardAllClickX:F3},{FastForwardAllClickY:F3}), " +
            $"then confirm at ({ConfirmClickX:F3},{ConfirmClickY:F3}).");
        await ctx.ClickAtPercent(FastForwardAllClickX, FastForwardAllClickY);
        await ctx.Wait(300);
        await ctx.ClickAtPercent(ConfirmClickX, ConfirmClickY);
        await ctx.Wait(1000);
    }

    public async Task ProbeAsync(GameContext ctx)
    {
        using var shot = ctx.CaptureScreen();
        if (shot == null || shot.Empty())
            return;

        int x = (int)(shot.Width * FastForwardAllClickX);
        int y = (int)(shot.Height * FastForwardAllClickY);
        Log.Log($"Event fast-forward settings probe: move all-events card point=({FastForwardAllClickX:F3},{FastForwardAllClickY:F3}) => ({x},{y})");
        await MouseSimulator.MoveToClient(ctx.WindowHandle, x, y);
        await ctx.Wait(200);
    }

    private static readonly LogScope Log = new("Race:FastForwardSettings");
}
