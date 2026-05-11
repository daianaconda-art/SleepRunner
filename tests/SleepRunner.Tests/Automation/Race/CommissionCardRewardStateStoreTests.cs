using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class CommissionCardRewardStateStoreTests
{
    [Fact]
    public void Red_difficult_commission_reward_marker_is_consumed_once()
    {
        object store = CreateStore();

        Assert.False(InvokeConsumeRedDifficultCommissionReward(store));

        InvokeMarkRedDifficultCommissionStarted(store);

        Assert.True(InvokeConsumeRedDifficultCommissionReward(store));
        Assert.False(InvokeConsumeRedDifficultCommissionReward(store));
    }

    private static object CreateStore()
    {
        Type type = GetStoreType();
        return Activator.CreateInstance(type)
            ?? throw new Xunit.Sdk.XunitException("Could not create CommissionCardRewardStateStore.");
    }

    private static void InvokeMarkRedDifficultCommissionStarted(object store)
    {
        MethodInfo method = store.GetType().GetMethod(
                                "MarkRedDifficultCommissionStarted",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("MarkRedDifficultCommissionStarted was not found.");

        method.Invoke(store, null);
    }

    private static bool InvokeConsumeRedDifficultCommissionReward(object store)
    {
        MethodInfo method = store.GetType().GetMethod(
                                "ConsumeRedDifficultCommissionReward",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("ConsumeRedDifficultCommissionReward was not found.");

        return (bool)method.Invoke(store, null)!;
    }

    private static Type GetStoreType()
    {
        return Type.GetType("SleepRunner.Automation.Race.Handlers.Commission.CommissionCardRewardStateStore, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("CommissionCardRewardStateStore type was not found.");
    }
}
