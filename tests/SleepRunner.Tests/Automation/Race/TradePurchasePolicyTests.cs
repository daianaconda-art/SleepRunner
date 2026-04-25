using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class TradePurchasePolicyTests
{
    [Fact]
    public void BuildPurchaseQueue_skips_strength_items_when_strength_is_capped()
    {
        object strengthBook = CreateTradeOffer(
            slotIndex: 0,
            price: 60,
            isMustBuy: true,
            affectsStrengthStat: true);
        object potentialPoints = CreateTradeOffer(
            slotIndex: 1,
            price: 40,
            isMustBuy: true,
            isPotentialPoint: true);
        object strengthFood = CreateTradeOffer(
            slotIndex: 2,
            price: 20,
            isStrengthIncrease: true,
            strengthGain: 18,
            affectsStrengthStat: true);

        IReadOnlyList<int> queue = InvokeBuildPurchaseQueue(
            [strengthBook, potentialPoints, strengthFood],
            preferStrengthItems: true,
            budget: 200,
            strengthStat: 1250,
            staminaStat: 400);

        Assert.Equal([1], queue);
    }

    [Fact]
    public void BuildPurchaseQueue_keeps_stamina_recovery_items_when_stamina_is_capped()
    {
        object staminaBook = CreateTradeOffer(
            slotIndex: 0,
            price: 60,
            isMustBuy: true,
            affectsStaminaStat: true);
        object donut = CreateTradeOffer(
            slotIndex: 1,
            price: 15,
            isMustBuy: true,
            isStaminaRecover: true);

        IReadOnlyList<int> queue = InvokeBuildPurchaseQueue(
            [staminaBook, donut],
            preferStrengthItems: true,
            budget: 200,
            strengthStat: 400,
            staminaStat: 1250);

        Assert.Equal([1], queue);
    }

    private static IReadOnlyList<int> InvokeBuildPurchaseQueue(
        object[] offers,
        bool preferStrengthItems,
        int budget,
        int? strengthStat,
        int? staminaStat)
    {
        Type offerType = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradeOffer, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradeOffer type was not found.");
        Type listType = typeof(List<>).MakeGenericType(offerType);
        object offerList = Activator.CreateInstance(listType)!
            ?? throw new Xunit.Sdk.XunitException("TradeOffer list could not be created.");
        MethodInfo addMethod = listType.GetMethod("Add")
            ?? throw new Xunit.Sdk.XunitException("TradeOffer list Add method was not found.");
        foreach (object offer in offers)
        {
            addMethod.Invoke(offerList, [offer]);
        }

        Type policyType = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradePurchasePolicy, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradePurchasePolicy type was not found.");
        MethodInfo method = policyType.GetMethod(
                                "BuildPurchaseQueue",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                                binder: null,
                                types:
                                [
                                    listType,
                                    typeof(bool),
                                    typeof(int),
                                    typeof(int?),
                                    typeof(int?)
                                ],
                                modifiers: null)
                            ?? throw new Xunit.Sdk.XunitException("TradePurchasePolicy.BuildPurchaseQueue overload was not found.");

        object? result = method.Invoke(null, [offerList, preferStrengthItems, budget, strengthStat, staminaStat]);
        Assert.NotNull(result);

        PropertyInfo slotIndexProperty = offerType.GetProperty("SlotIndex")
            ?? throw new Xunit.Sdk.XunitException("TradeOffer.SlotIndex property was not found.");

        var queueSlots = new List<int>();
        foreach (object queueItem in (System.Collections.IEnumerable)result!)
        {
            queueSlots.Add((int)slotIndexProperty.GetValue(queueItem)!);
        }

        return queueSlots;
    }

    private static object CreateTradeOffer(
        int slotIndex,
        int price,
        bool isMustBuy = false,
        bool isPotentialPoint = false,
        bool isStrengthIncrease = false,
        int strengthGain = 0,
        bool isStaminaRecover = false,
        bool affectsStrengthStat = false,
        bool affectsStaminaStat = false)
    {
        Type offerType = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradeOffer, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradeOffer type was not found.");
        object offer = Activator.CreateInstance(offerType)!
            ?? throw new Xunit.Sdk.XunitException("TradeOffer could not be created.");

        SetProperty(offerType, offer, "SlotIndex", slotIndex);
        SetProperty(offerType, offer, "Price", price);
        SetProperty(offerType, offer, "IsMustBuy", isMustBuy);
        SetProperty(offerType, offer, "IsPotentialPoint", isPotentialPoint);
        SetProperty(offerType, offer, "IsStrengthIncrease", isStrengthIncrease);
        SetProperty(offerType, offer, "StrengthGain", strengthGain);
        SetProperty(offerType, offer, "IsStaminaRecover", isStaminaRecover);
        SetProperty(offerType, offer, "AffectsStrengthStat", affectsStrengthStat);
        SetProperty(offerType, offer, "AffectsStaminaStat", affectsStaminaStat);
        SetProperty(offerType, offer, "HasBuyButtonVisible", true);
        SetProperty(offerType, offer, "IsBuyDisabled", false);
        SetProperty(offerType, offer, "SlotText", $"slot-{slotIndex + 1}");
        SetProperty(offerType, offer, "EffectText", $"effect-{slotIndex + 1}");
        return offer;
    }

    private static void SetProperty(Type type, object instance, string name, object value)
    {
        PropertyInfo property = type.GetProperty(name)
            ?? throw new Xunit.Sdk.XunitException($"TradeOffer.{name} property was not found.");
        property.SetValue(instance, value);
    }
}
