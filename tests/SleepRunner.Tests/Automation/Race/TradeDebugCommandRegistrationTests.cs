using System.Reflection;
using System.Runtime.Loader;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class TradeDebugCommandRegistrationTests
{
    [Fact]
    public void CreateDefault_registers_debug_trade_flow_command()
    {
        Assembly asm = LoadSleepRunnerAssembly();
        Type dispatcherType = asm.GetType("SleepRunner.Cli.CliDispatcher")
            ?? throw new Xunit.Sdk.XunitException("CliDispatcher type was not found.");
        MethodInfo createDefault = dispatcherType.GetMethod(
                                       "CreateDefault",
                                       BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                   ?? throw new Xunit.Sdk.XunitException("CliDispatcher.CreateDefault was not found.");
        object dispatcher = createDefault.Invoke(null, [])!;
        MethodInfo tryResolve = dispatcherType.GetMethod(
                                    "TryResolve",
                                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                ?? throw new Xunit.Sdk.XunitException("CliDispatcher.TryResolve was not found.");

        object?[] args = [new[] { "--debug-trade-flow" }, null];
        bool resolved = (bool)tryResolve.Invoke(dispatcher, args)!;

        Assert.True(resolved);
    }

    private static Assembly LoadSleepRunnerAssembly()
    {
        string? overridePath = Environment.GetEnvironmentVariable("STAR_SAVIOR_TEST_ASSEMBLY_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            var alc = new AssemblyLoadContext($"trade-cli-tests-{Guid.NewGuid():N}", isCollectible: true);
            return alc.LoadFromAssemblyPath(overridePath);
        }

        return Assembly.Load("SleepRunner");
    }
}
