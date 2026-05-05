using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class TradeBuyActionsTests
{
    private static readonly OpCode[] SingleByteOpCodes = new OpCode[0x100];
    private static readonly OpCode[] MultiByteOpCodes = new OpCode[0x100];

    static TradeBuyActionsTests()
    {
        foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opCode)
                continue;

            ushort value = (ushort)opCode.Value;
            if (value < 0x100)
                SingleByteOpCodes[value] = opCode;
            else if ((value & 0xFF00) == 0xFE00)
                MultiByteOpCodes[value & 0xFF] = opCode;
        }
    }

    [Fact]
    public void TryOpenOfferDetailAsync_attempts_slot_hotkey_before_mouse_fallback()
    {
        MethodInfo moveNext = GetAsyncMoveNext("TryOpenOfferDetailAsync");
        MethodBase[] calls = GetCalledMethods(moveNext).ToArray();

        int hotkeyIndex = Array.FindIndex(calls, IsSendGameAction);
        int mouseClickIndex = Array.FindIndex(calls, IsClickAtPercent);

        Assert.True(hotkeyIndex >= 0, "TryOpenOfferDetailAsync should attempt a slot hotkey through GameContext.SendGameAction.");
        Assert.True(mouseClickIndex >= 0, "TryOpenOfferDetailAsync should keep the mouse click fallback.");
        Assert.True(
            hotkeyIndex < mouseClickIndex,
            "TryOpenOfferDetailAsync should try the slot hotkey before falling back to mouse row click.");
    }

    [Fact]
    public void TryOpenOfferDetailAsync_uses_unscaled_detail_settle_waits()
    {
        MethodInfo moveNext = GetAsyncMoveNext("TryOpenOfferDetailAsync");
        MethodBase[] calls = GetCalledMethods(moveNext).ToArray();

        int unscaledWaits = calls.Count(IsWaitUnscaled);
        int scaledWaits = calls.Count(IsWait);

        Assert.True(
            unscaledWaits >= 2,
            "Trade detail opening should wait with an unscaled delay after the slot hotkey and after the row-click fallback.");
        Assert.Equal(0, scaledWaits);
    }

    [Fact]
    public void ScanOfferWithRetriesAsync_uses_opened_detail_snapshot_without_fresh_recapture()
    {
        MethodInfo moveNext = GetAsyncMoveNext(
            "SleepRunner.Automation.Race.Handlers.Trade.DefaultTradeFlowExecutor",
            "ScanOfferWithRetriesAsync");
        MethodBase[] calls = GetCalledMethods(moveNext).ToArray();

        int openIndex = Array.FindIndex(calls, IsTryOpenOfferDetail);
        int buildIndex = Array.FindIndex(calls, openIndex + 1, IsBuildOfferFromShot);

        Assert.True(openIndex >= 0, "ScanOfferWithRetriesAsync should open the detail view through TradeBuyActions.");
        Assert.True(buildIndex > openIndex, "ScanOfferWithRetriesAsync should build an offer from the opened detail snapshot.");

        bool recapturedBeforeBuild = calls
            .Skip(openIndex + 1)
            .Take(buildIndex - openIndex - 1)
            .Any(IsCaptureScreen);
        Assert.False(
            recapturedBeforeBuild,
            "ScanOfferWithRetriesAsync should not immediately recapture after a validated detail-open snapshot; fresh OCR jitter can discard the selected slot.");
    }

    [Fact]
    public void ScanOfferWithRetriesAsync_checks_sold_out_row_before_building_detail_offer()
    {
        MethodInfo moveNext = GetAsyncMoveNext(
            "SleepRunner.Automation.Race.Handlers.Trade.DefaultTradeFlowExecutor",
            "ScanOfferWithRetriesAsync");
        MethodBase[] calls = GetCalledMethods(moveNext).ToArray();

        int soldOutFallbackIndex = Array.FindIndex(calls, IsTryBuildSoldOutRowFallback);
        int buildIndex = Array.FindIndex(calls, IsBuildOfferFromShot);

        Assert.True(
            soldOutFallbackIndex >= 0,
            "ScanOfferWithRetriesAsync should check whether a sold-out row can be resolved before running full detail OCR.");
        Assert.True(
            buildIndex > soldOutFallbackIndex,
            "Sold-out row fallback should run before TradePurchasePolicy.BuildOfferFromShot so sold-out items skip expensive detail OCR.");
    }

    [Fact]
    public void ScanOfferWithRetriesAsync_defers_unowned_detail_before_building_detail_offer()
    {
        MethodInfo moveNext = GetAsyncMoveNext(
            "SleepRunner.Automation.Race.Handlers.Trade.DefaultTradeFlowExecutor",
            "ScanOfferWithRetriesAsync");
        MethodBase[] calls = GetCalledMethods(moveNext).ToArray();

        int deferIndex = Array.FindIndex(calls, IsShouldDeferUnownedDetailScan);
        int buildIndex = Array.FindIndex(calls, IsBuildOfferFromShot);

        Assert.True(
            deferIndex >= 0,
            "ScanOfferWithRetriesAsync should defer full detail OCR when the opened detail is not yet owned by the requested slot.");
        Assert.True(
            buildIndex > deferIndex,
            "The unowned-detail defer check should happen before TradePurchasePolicy.BuildOfferFromShot.");
    }

    [Fact]
    public void ScanOfferWithRetriesAsync_builds_offer_with_cached_row_and_detail_text()
    {
        MethodInfo moveNext = GetAsyncMoveNext(
            "SleepRunner.Automation.Race.Handlers.Trade.DefaultTradeFlowExecutor",
            "ScanOfferWithRetriesAsync");
        MethodBase[] calls = GetCalledMethods(moveNext).ToArray();

        Assert.Contains(calls, IsBuildOfferFromShotWithCachedText);
    }

    [Fact]
    public void GetBuyFallbackClickPoints_uses_fixed_buy_button_click_points()
    {
        IReadOnlyList<(double X, double Y)> points = InvokeGetBuyFallbackClickPoints();

        Assert.Equal(
            [
                (0.82, 0.88),
                (0.80, 0.86),
                (0.84, 0.90),
                (0.89, 0.89),
                (0.86, 0.89),
                (0.91, 0.85)
            ],
            points);
    }

    [Fact]
    public void ReadBuyabilityState_marks_row_sold_out_offer_as_purchased()
    {
        object offer = CreateTradeOffer();
        Type offerType = offer.GetType();
        SetProperty(offerType, offer, "HasBuyButtonVisible", false);
        SetProperty(offerType, offer, "IsBuyDisabled", false);
        SetProperty(offerType, offer, "IsRowSoldOut", true);

        string state = InvokeReadBuyabilityState(offer);

        Assert.Equal("Purchased", state);
    }

    private static MethodInfo GetAsyncMoveNext(string methodName)
    {
        return GetAsyncMoveNext("SleepRunner.Automation.Race.Handlers.Trade.TradeBuyActions", methodName);
    }

    private static MethodInfo GetAsyncMoveNext(string typeName, string methodName)
    {
        Type targetType = Type.GetType($"{typeName}, SleepRunner")
                          ?? throw new Xunit.Sdk.XunitException($"{typeName} type was not found.");
        MethodInfo method = targetType.GetMethod(
                                methodName,
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException($"{typeName}.{methodName} was not found.");

        Type stateMachineType = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
                                ?? throw new Xunit.Sdk.XunitException($"{typeName}.{methodName} has no async state machine.");
        return stateMachineType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
               ?? throw new Xunit.Sdk.XunitException($"TradeBuyActions.{methodName} MoveNext was not found.");
    }

    private static IEnumerable<MethodBase> GetCalledMethods(MethodInfo method)
    {
        byte[] il = method.GetMethodBody()?.GetILAsByteArray()
                    ?? throw new Xunit.Sdk.XunitException($"{method.Name} has no IL body.");
        Module module = method.Module;

        for (int offset = 0; offset < il.Length;)
        {
            OpCode opCode = ReadOpCode(il, ref offset);
            if (opCode.OperandType == OperandType.InlineMethod)
            {
                int token = BitConverter.ToInt32(il, offset);
                offset += 4;
                MethodBase? calledMethod = module.ResolveMethod(token);
                if (calledMethod != null)
                    yield return calledMethod;
                continue;
            }

            offset += GetOperandSize(opCode.OperandType, il, offset);
        }
    }

    private static OpCode ReadOpCode(byte[] il, ref int offset)
    {
        byte value = il[offset++];
        if (value != 0xFE)
            return SingleByteOpCodes[value];

        return MultiByteOpCodes[il[offset++]];
    }

    private static int GetOperandSize(OperandType operandType, byte[] il, int offset)
    {
        return operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget => 1,
            OperandType.ShortInlineI => 1,
            OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget => 4,
            OperandType.InlineField => 4,
            OperandType.InlineI => 4,
            OperandType.InlineSig => 4,
            OperandType.InlineString => 4,
            OperandType.InlineTok => 4,
            OperandType.InlineType => 4,
            OperandType.ShortInlineR => 4,
            OperandType.InlineI8 => 8,
            OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + BitConverter.ToInt32(il, offset) * 4,
            _ => throw new Xunit.Sdk.XunitException($"Unsupported IL operand type: {operandType}."),
        };
    }

    private static bool IsSendGameAction(MethodBase method)
    {
        return method.Name == "SendGameAction" &&
               method.DeclaringType?.FullName == "SleepRunner.Automation.GameContext";
    }

    private static bool IsClickAtPercent(MethodBase method)
    {
        return method.Name == "ClickAtPercent" &&
               method.DeclaringType?.FullName == "SleepRunner.Automation.GameContext";
    }

    private static bool IsWaitUnscaled(MethodBase method)
    {
        return method.Name == "WaitUnscaled" &&
               method.DeclaringType?.FullName == "SleepRunner.Automation.GameContext";
    }

    private static bool IsWait(MethodBase method)
    {
        return method.Name == "Wait" &&
               method.DeclaringType?.FullName == "SleepRunner.Automation.GameContext";
    }

    private static bool IsCaptureScreen(MethodBase method)
    {
        return method.Name == "CaptureScreen" &&
               method.DeclaringType?.FullName == "SleepRunner.Automation.GameContext";
    }

    private static bool IsTryOpenOfferDetail(MethodBase method)
    {
        return method.Name == "TryOpenOfferDetailAsync" &&
               method.DeclaringType?.FullName == "SleepRunner.Automation.Race.Handlers.Trade.TradeBuyActions";
    }

    private static bool IsBuildOfferFromShot(MethodBase method)
    {
        return method.Name == "BuildOfferFromShot" &&
               method.DeclaringType?.FullName == "SleepRunner.Automation.Race.Handlers.Trade.TradePurchasePolicy";
    }

    private static bool IsBuildOfferFromShotWithCachedText(MethodBase method)
    {
        return method.Name == "BuildOfferFromShot" &&
               method.DeclaringType?.FullName == "SleepRunner.Automation.Race.Handlers.Trade.TradePurchasePolicy" &&
               method.GetParameters() is { Length: 4 } parameters &&
               parameters[2].ParameterType == typeof(string) &&
               parameters[3].ParameterType == typeof(string);
    }

    private static bool IsTryBuildSoldOutRowFallback(MethodBase method)
    {
        return method.Name == "TryBuildSoldOutRowFallback" &&
               method.DeclaringType?.FullName == "SleepRunner.Automation.Race.Handlers.Trade.TradePurchasePolicy";
    }

    private static bool IsShouldDeferUnownedDetailScan(MethodBase method)
    {
        return method.Name == "ShouldDeferUnownedDetailScan" &&
               method.DeclaringType?.FullName == "SleepRunner.Automation.Race.Handlers.Trade.DefaultTradeFlowExecutor";
    }

    private static IReadOnlyList<(double X, double Y)> InvokeGetBuyFallbackClickPoints()
    {
        Type actionsType = GetTradeBuyActionsType();
        MethodInfo method = actionsType.GetMethod(
                                "GetBuyFallbackClickPoints",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradeBuyActions.GetBuyFallbackClickPoints was not found.");

        return ((IEnumerable<(double X, double Y)>)method.Invoke(null, [])!).ToArray();
    }

    private static string InvokeReadBuyabilityState(object offer)
    {
        Type actionsType = GetTradeBuyActionsType();
        MethodInfo method = actionsType.GetMethod(
                                "ReadBuyabilityState",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradeBuyActions.ReadBuyabilityState was not found.");

        return method.Invoke(null, [offer])!.ToString()!;
    }

    private static object CreateTradeOffer()
    {
        Type offerType = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradeOffer, SleepRunner")
                         ?? throw new Xunit.Sdk.XunitException("TradeOffer type was not found.");
        object offer = Activator.CreateInstance(offerType)!
                       ?? throw new Xunit.Sdk.XunitException("TradeOffer could not be created.");

        SetProperty(offerType, offer, "SlotIndex", 0);
        SetProperty(offerType, offer, "SlotText", "sold-out-row");
        SetProperty(offerType, offer, "EffectText", "effect");
        return offer;
    }

    private static void SetProperty(Type type, object instance, string name, object value)
    {
        PropertyInfo property = type.GetProperty(name)
                                ?? throw new Xunit.Sdk.XunitException($"TradeOffer.{name} property was not found.");
        property.SetValue(instance, value);
    }

    private static Type GetTradeBuyActionsType()
    {
        return Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradeBuyActions, SleepRunner")
               ?? throw new Xunit.Sdk.XunitException("TradeBuyActions type was not found.");
    }
}
