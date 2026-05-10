using System.Reflection;
using System.Text.Json;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class TradePurchasePolicyTests
{
    [Fact]
    public void DefaultTradeProfile_does_not_buy_stamina_stat_increase_items()
    {
        string profilePath = Path.Combine(AppContext.BaseDirectory, "assets", "trade", "default.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(profilePath));
        string[] keywords = doc.RootElement
            .GetProperty("keywords")
            .EnumerateArray()
            .Select(e => e.GetString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .ToArray();

        Assert.DoesNotContain("体力", keywords);
    }

    [Fact]
    public void SurvivalTradeProfile_buys_energy_drink_by_name()
    {
        string profilePath = Path.Combine(AppContext.BaseDirectory, "assets", "trade", "survival.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(profilePath));
        string[] keywords = doc.RootElement
            .GetProperty("keywords")
            .EnumerateArray()
            .Select(e => e.GetString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .ToArray();

        Assert.Contains("能量饮料", keywords);
    }

    [Fact]
    public void AttackTradeProfile_buys_only_strength_training_books_not_all_books()
    {
        string profilePath = Path.Combine(AppContext.BaseDirectory, "assets", "trade", "attack.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(profilePath));
        string[] keywords = doc.RootElement
            .GetProperty("keywords")
            .EnumerateArray()
            .Select(e => e.GetString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .ToArray();

        Assert.DoesNotContain("禁书", keywords);
        Assert.Contains("力量训练的禁书", keywords);
    }

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

    [Fact]
    public void IsOfferReadable_allows_strong_detail_effect_when_slot_text_is_unreliable()
    {
        object strengthOffer = CreateTradeOffer(
            slotIndex: 1,
            price: 16,
            isStrengthIncrease: true,
            strengthGain: 12,
            affectsStrengthStat: true);
        Type offerType = strengthOffer.GetType();
        SetProperty(offerType, strengthOffer, "HasReliableSlotText", false);
        SetProperty(offerType, strengthOffer, "EffectText", "效果力量增加12");
        SetProperty(offerType, strengthOffer, "SlotText", "");

        Assert.True(InvokeIsOfferReadable(strengthOffer));
    }

    [Fact]
    public void IsStrongDetailPurchaseCandidate_accepts_strength_effect_from_clicked_detail()
    {
        object strengthOffer = CreateTradeOffer(
            slotIndex: 1,
            price: 16,
            isStrengthIncrease: true,
            strengthGain: 12,
            affectsStrengthStat: true);
        Type offerType = strengthOffer.GetType();
        SetProperty(offerType, strengthOffer, "HasReliableSlotText", false);
        SetProperty(offerType, strengthOffer, "EffectText", "效果力量增加12");
        SetProperty(offerType, strengthOffer, "SlotText", "");

        Assert.True(InvokeIsStrongDetailPurchaseCandidate(strengthOffer));
    }

    [Fact]
    public void IsStrongDetailPurchaseCandidate_accepts_reliable_keyword_slot_when_effect_ocr_is_empty()
    {
        object energyDrink = CreateTradeOffer(
            slotIndex: 2,
            price: 20,
            isMustBuy: true);
        Type offerType = energyDrink.GetType();
        SetProperty(offerType, energyDrink, "SlotText", "能量饮料20");
        SetProperty(offerType, energyDrink, "EffectText", "");
        SetProperty(offerType, energyDrink, "HasReliableSlotText", true);

        Assert.True(InvokeIsStrongDetailPurchaseCandidate(energyDrink));
    }

    [Fact]
    public void SelectSlotText_prefers_row_when_detail_title_does_not_match_requested_slot()
    {
        string selected = InvokeSelectSlotText("能量饮料20", "not0因n了俨bJS然孓");

        Assert.Equal("能量饮料20", selected);
    }

    [Theory]
    [InlineData("目匕量饣欠*斗一丿20", "能量饮料")]
    [InlineData("能量饮料．·。乸20", "能量饮料")]
    public void NormalizeTradeSignalText_maps_energy_drink_ocr_noise(string raw, string expectedFragment)
    {
        string normalized = InvokeNormalizeTradeSignalText(raw);

        Assert.Contains(expectedFragment, normalized);
    }

    [Fact]
    public void ContainsTradeItemKeyword_accepts_energy_drink_ocr_noise()
    {
        Assert.True(InvokeContainsTradeItemKeyword("目匕量饣欠*斗一丿20"));
    }

    [Fact]
    public void ShouldScanBuyButtonState_skips_reliable_no_signal_offers()
    {
        bool shouldScan = InvokeShouldScanBuyButtonState(
            hasReliableSlotText: true,
            isPotentialPoint: false,
            isMustBuy: false,
            isStrengthIncrease: false,
            isStaminaRecover: false);

        Assert.False(shouldScan);
    }

    [Fact]
    public void ShouldScanBuyButtonState_keeps_purchase_candidates()
    {
        bool shouldScan = InvokeShouldScanBuyButtonState(
            hasReliableSlotText: true,
            isPotentialPoint: false,
            isMustBuy: true,
            isStrengthIncrease: false,
            isStaminaRecover: false);

        Assert.True(shouldScan);
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

    private static bool InvokeIsOfferReadable(object offer)
    {
        Type policyType = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradePurchasePolicy, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradePurchasePolicy type was not found.");
        MethodInfo method = policyType.GetMethod(
                                "IsOfferReadable",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradePurchasePolicy.IsOfferReadable was not found.");

        return (bool)method.Invoke(null, [offer])!;
    }

    private static bool InvokeIsStrongDetailPurchaseCandidate(object offer)
    {
        Type policyType = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradePurchasePolicy, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradePurchasePolicy type was not found.");
        MethodInfo method = policyType.GetMethod(
                                "IsStrongDetailPurchaseCandidate",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradePurchasePolicy.IsStrongDetailPurchaseCandidate was not found.");

        return (bool)method.Invoke(null, [offer])!;
    }

    private static bool InvokeShouldScanBuyButtonState(
        bool hasReliableSlotText,
        bool isPotentialPoint,
        bool isMustBuy,
        bool isStrengthIncrease,
        bool isStaminaRecover)
    {
        Type policyType = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradePurchasePolicy, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradePurchasePolicy type was not found.");
        MethodInfo method = policyType.GetMethod(
                                "ShouldScanBuyButtonState",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradePurchasePolicy.ShouldScanBuyButtonState was not found.");

        return (bool)method.Invoke(
            null,
            [hasReliableSlotText, isPotentialPoint, isMustBuy, isStrengthIncrease, isStaminaRecover])!;
    }

    private static string InvokeSelectSlotText(string rowSlotText, string detailTitle)
    {
        Type policyType = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradePurchasePolicy, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradePurchasePolicy type was not found.");
        MethodInfo method = policyType.GetMethod(
                                "SelectSlotText",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradePurchasePolicy.SelectSlotText was not found.");

        return (string)method.Invoke(null, [rowSlotText, detailTitle])!;
    }

    private static string InvokeNormalizeTradeSignalText(string raw)
    {
        Type policyType = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradePurchasePolicy, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradePurchasePolicy type was not found.");
        MethodInfo method = policyType.GetMethod(
                                "NormalizeTradeSignalText",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradePurchasePolicy.NormalizeTradeSignalText was not found.");

        return (string)method.Invoke(null, [raw])!;
    }

    private static bool InvokeContainsTradeItemKeyword(string raw)
    {
        Type policyType = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradePurchasePolicy, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradePurchasePolicy type was not found.");
        MethodInfo method = policyType.GetMethod(
                                "ContainsTradeItemKeyword",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradePurchasePolicy.ContainsTradeItemKeyword was not found.");

        return (bool)method.Invoke(null, [raw])!;
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
