using OpenCvSharp;
using SleepRunner.Automation;
using SleepRunner.Automation.Race.Handlers.Training;
using SleepRunner.Utils;

namespace SleepRunner.Automation.Race;

internal static class MainMenuTransitionWaiter
{
    private const int TrainingMaxAttempts = 12;
    private const int TrainingPollMs = 120;

    public static async Task<bool> WaitForTrainingScreenAsync(GameContext ctx)
    {
        int attempt = 0;
        bool result = await WaitUntilAsync(
            _ =>
            {
                attempt++;
                using Mat? shot = ctx.CaptureScreen();
                if (shot == null || shot.Empty())
                    return Task.FromResult(false);

                string branchText = TrainingScreenChecks.ReadBranchText(shot);
                bool matched = TrainingScreenChecks.IsTrainingDetailText(branchText);
                if (matched)
                    Log.Log($"Training transition hit on attempt {attempt}/{TrainingMaxAttempts}.");
                return Task.FromResult(matched);
            },
            async (ms, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ctx.Wait(ms);
            },
            ctx.CancellationToken,
            TrainingMaxAttempts,
            TrainingPollMs);

        if (!result)
            Log.Log($"Training transition wait timed out after {TrainingMaxAttempts} attempts x {TrainingPollMs}ms.");

        return result;
    }

    internal static async Task<bool> WaitUntilAsync(
        Func<CancellationToken, Task<bool>> probeAsync,
        Func<int, CancellationToken, Task> delayAsync,
        CancellationToken cancellationToken,
        int maxAttempts,
        int pollMs)
    {
        if (maxAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        if (pollMs < 0)
            throw new ArgumentOutOfRangeException(nameof(pollMs));

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await probeAsync(cancellationToken))
                return true;

            if (attempt < maxAttempts)
                await delayAsync(pollMs, cancellationToken);
        }

        return false;
    }

    private static readonly LogScope Log = new("Race:MainMenuWait");
}
