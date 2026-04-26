using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class MainMenuRestClickPointTests
{
    [Fact]
    public void ResolveRestMenuClickPoint_falls_back_to_current_rest_main_menu_row()
    {
        var point = InvokeResolveRestMenuClickPoint();

        Assert.Equal(0.91d, point.X, 3);
        Assert.Equal(0.626d, point.Y, 3);
    }

    private static (double X, double Y) InvokeResolveRestMenuClickPoint()
    {
        Type checksType = Type.GetType("SleepRunner.Automation.Race.Handlers.MainMenuScreenChecks, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("MainMenuScreenChecks type was not found.");

        MethodInfo method = checksType.GetMethod(
                                "ResolveRestMenuClickPoint",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("MainMenuScreenChecks.ResolveRestMenuClickPoint was not found.");

        return ((double X, double Y))method.Invoke(null, [null])!;
    }
}
