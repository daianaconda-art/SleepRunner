using SleepRunner.Automation;
using SleepRunner.Automation.Race;
using SleepRunner.Automation.Race.Handlers;
using SleepRunner.Automation.Race.Policy.Training;
using SleepRunner.Capture;
using SleepRunner.Utils;
using SleepRunner.Vision;

namespace SleepRunner.Cli.Commands;

internal sealed class TestTrainingCommand : ICliCommand
{
    public string Name => "--test-training";

    public async Task<int> ExecuteAsync(string[] args)
    {
        CliBootstrap.EnsureUtf8Console();
        var settings = UserSettings.Load();
        settings.ApplyToRaceConfig();

        Console.WriteLine("=== --test-training: scan 5 rows, no Train click ===");
        Console.WriteLine($"Base dir: {PathHelper.BaseDir}");

        var hWnd = CliBootstrap.FindGameOrLogError();
        if (hWnd == IntPtr.Zero)
            return 1;

        var (w, h) = WindowHelper.GetClientSize(hWnd);
        Console.WriteLine($"Game client: {w}x{h}");

        using var capture = new BitBltCapture();
        capture.Start(hWnd);

        using var cts = new CancellationTokenSource();
        using var ctx = new GameContext(hWnd, capture, cts.Token);

        var handler = new TrainingSelectHandler();
        using (var shot = ctx.CaptureScreen())
        {
            if (shot == null || shot.Empty())
            {
                Console.WriteLine("ERROR: Capture failed");
                return 1;
            }

            var frame = new FrameContext(shot);
            if (!handler.CanHandle(frame))
            {
                Console.WriteLine("NOT training select screen (training OCR fingerprint did not match).");
                return 0;
            }
        }

        Console.WriteLine("Training screen detected. Keep game focused; scanning rows...");
        await Task.Delay(500);

        var snapshot = await handler.RunFullScanAsync(ctx);
        string profileName = TrainingRuleProfileManager.CurrentProfile;
        TrainingRuleProfile profile = TrainingRuleStore.CurrentProfile;
        TrainingDecisionContext decisionContext = snapshot.ToDecisionContext(profile, profileName);
        TrainingDecisionResult decision = TrainingRuleEngine.Evaluate(decisionContext, profile);

        Console.WriteLine("---");
        Console.WriteLine($"profile = {profileName}");
        Console.WriteLine($"icons = [{string.Join(", ", snapshot.IconCounts)}]");
        Console.WriteLine($"fails = [{string.Join(", ", snapshot.FailRates)}]");
        Console.WriteLine($"stats = strength:{FormatStat(snapshot.StrengthStat)}, stamina:{FormatStat(snapshot.StaminaStat)}");
        Console.WriteLine($"matched rule = {decision.MatchedRuleId}");
        Console.WriteLine($"action = {decision.Action}");
        Console.WriteLine($"summary = {decision.Summary}");
        Console.WriteLine("Done (did not click bottom Train).");
        return 0;
    }

    private static string FormatStat(int? value)
    {
        return value?.ToString() ?? "N/A";
    }
}
