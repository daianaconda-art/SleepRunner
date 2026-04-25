using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class MainMenuTransitionWaiterTests
{
    [Fact]
    public async Task WaitUntilAsync_stops_polling_once_probe_hits()
    {
        Type waiterType = GetWaiterType();
        MethodInfo method = GetWaitUntilAsync(waiterType);

        int probeCount = 0;
        List<int> delays = [];

        Func<CancellationToken, Task<bool>> probeAsync = _ =>
        {
            probeCount++;
            return Task.FromResult(probeCount >= 3);
        };

        Func<int, CancellationToken, Task> delayAsync = (ms, _) =>
        {
            delays.Add(ms);
            return Task.CompletedTask;
        };

        var task = (Task<bool>)method.Invoke(
            null,
            [probeAsync, delayAsync, CancellationToken.None, 6, 120])!;

        bool result = await task;

        Assert.True(result);
        Assert.Equal(3, probeCount);
        Assert.Equal([120, 120], delays);
    }

    [Fact]
    public async Task WaitUntilAsync_returns_false_after_max_attempts()
    {
        Type waiterType = GetWaiterType();
        MethodInfo method = GetWaitUntilAsync(waiterType);

        int probeCount = 0;
        List<int> delays = [];

        Func<CancellationToken, Task<bool>> probeAsync = _ =>
        {
            probeCount++;
            return Task.FromResult(false);
        };

        Func<int, CancellationToken, Task> delayAsync = (ms, _) =>
        {
            delays.Add(ms);
            return Task.CompletedTask;
        };

        var task = (Task<bool>)method.Invoke(
            null,
            [probeAsync, delayAsync, CancellationToken.None, 4, 90])!;

        bool result = await task;

        Assert.False(result);
        Assert.Equal(4, probeCount);
        Assert.Equal([90, 90, 90], delays);
    }

    private static Type GetWaiterType()
    {
        return Type.GetType("SleepRunner.Automation.Race.MainMenuTransitionWaiter, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("MainMenuTransitionWaiter type was not found.");
    }

    private static MethodInfo GetWaitUntilAsync(Type waiterType)
    {
        return waiterType.GetMethod(
                   "WaitUntilAsync",
                   BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
               ?? throw new Xunit.Sdk.XunitException("WaitUntilAsync method was not found.");
    }
}
