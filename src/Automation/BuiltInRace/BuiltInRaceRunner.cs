using SleepRunner.Utils;

namespace SleepRunner.Automation.BuiltInRace;

public sealed class BuiltInRaceRunner : IGameTask
{
    private const int MaxIdleTicks = 120;
    private readonly Action<string>? _activityChanged;

    public string Name => "内置跑马";

    public BuiltInRaceRunner(Action<string>? activityChanged = null)
    {
        _activityChanged = activityChanged;
    }

    public async Task RunAsync(GameContext ctx)
    {
        Log.Log("=== Built-in Race Automation: Start ===");
        Report("识别内置跑马界面...");

        int idleTicks = 0;
        bool keepPollingForCompletion = false;
        while (true)
        {
            ctx.CheckCancellation();

            using var shot = ctx.CaptureScreen();
            if (shot == null || shot.Empty())
            {
                idleTicks++;
                Report("等待截图...");
                await ctx.Wait(800);
                continue;
            }

            BuiltInRaceScreenSnapshot snapshot = BuiltInRaceScreenReader.Read(shot);
            BuiltInRaceAction? action = BuiltInRacePlanner.Decide(snapshot);
            if (action is null)
            {
                idleTicks++;
                if (!keepPollingForCompletion && idleTicks >= MaxIdleTicks)
                {
                    Log.Log("No built-in race screen matched for too long, stopping.");
                    break;
                }

                Report(keepPollingForCompletion ? "等待旅程结算..." : "等待可识别界面...");
                await ctx.Wait(800);
                continue;
            }

            idleTicks = 0;
            Log.Log($"Action: {action.Value.Step} ({action.Value.Description}) at ({action.Value.XPct:F3},{action.Value.YPct:F3})");
            Report(action.Value.Description);
            await ctx.ClickAtPercent(action.Value.XPct, action.Value.YPct);

            if (action.Value.Step is BuiltInRaceStep.StartAutoJourney or BuiltInRaceStep.ConfirmEntry)
            {
                keepPollingForCompletion = true;
            }

            if (action.Value.Step == BuiltInRaceStep.ConfirmEntry)
            {
                await ctx.Wait(800);
                Report("已启动内置自动旅程，等待结算...");
                continue;
            }

            if (BuiltInRacePlanner.ShouldStopAfterAction(action.Value.Step))
            {
                await ctx.Wait(800);
                break;
            }

            await ctx.Wait(1000);
        }

        Log.Log("=== Built-in Race Automation: Done ===");
    }

    private void Report(string text)
    {
        try
        {
            _activityChanged?.Invoke(text);
        }
        catch (Exception ex)
        {
            Log.Log($"Activity callback threw: {ex.Message}");
        }
    }

    private static readonly LogScope Log = new("BuiltInRace");
}
