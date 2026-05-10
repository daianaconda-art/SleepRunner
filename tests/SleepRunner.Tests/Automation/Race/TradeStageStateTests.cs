using System.Reflection;
using OpenCvSharp;
using SleepRunner.Automation;
using SleepRunner.Automation.Race;
using SleepRunner.Automation.Race.Handlers;
using SleepRunner.Automation.Race.Handlers.Trade;
using SleepRunner.Vision;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class TradeStageStateTests
{
    [Fact]
    public void TradeAndAppraise_decision_reflects_state_store_after_state_is_cleared()
    {
        ClearPersistedState();
        try
        {
            object setupStore = CreateTradeStateStore();
            InvokeSaveVisited(setupStore, true);
            var handler = new TradeAndAppraiseHandler();

            Assert.Contains("trade already visited", Describe(handler));

            object handlerStore = GetField(handler, "_stateStore");
            InvokeSaveVisited(handlerStore, false);

            Assert.Contains("enter trade first", Describe(handler));
        }
        finally
        {
            ClearPersistedState();
        }
    }

    [Fact]
    public void RaceRunner_shares_trade_state_between_appraise_accept_and_trade_stage_handlers()
    {
        ClearPersistedState();
        try
        {
            var runner = new RaceRunner();
            object appraiseHandler = GetRegisteredHandler(runner, "AppraiseAcceptHandler");
            object tradeHandler = GetRegisteredHandler(runner, "TradeAndAppraiseHandler");

            object appraiseStore = GetField(appraiseHandler, "_tradeStateStore");
            object tradeStore = GetField(tradeHandler, "_stateStore");

            Assert.Same(tradeStore, appraiseStore);
        }
        finally
        {
            ClearPersistedState();
        }
    }

    [Fact]
    public void RaceRunner_shares_trade_state_with_trade_purchase_handler()
    {
        ClearPersistedState();
        try
        {
            var runner = new RaceRunner();
            object tradeStageHandler = GetRegisteredHandler(runner, "TradeAndAppraiseHandler");
            object tradePurchaseHandler = GetRegisteredHandler(runner, "TradePurchaseHandler");

            object tradeStageStore = GetField(tradeStageHandler, "_stateStore");
            object tradePurchaseStore = GetField(tradePurchaseHandler, "_tradeStateStore");

            Assert.Same(tradeStageStore, tradePurchaseStore);
        }
        finally
        {
            ClearPersistedState();
        }
    }

    [Fact]
    public async Task TradePurchaseHandler_marks_trade_visited_before_exit_cleanup()
    {
        ClearPersistedState();
        try
        {
            object stateStore = CreateTradeStateStore();
            var handler = CreateTradePurchaseHandler(
                new StaticTradeExecutor(TradeExecutionResult.NoPurchase),
                stateStore);

            try
            {
                await handler.HandleAsync(null!);
            }
            catch
            {
                // The test only needs to prove the durable state is written before
                // later screen cleanup work touches GameContext.
            }

            Assert.True(InvokeLoadVisited(stateStore));
        }
        finally
        {
            ClearPersistedState();
        }
    }

    private static string Describe(TradeAndAppraiseHandler handler)
    {
        using var screenshot = new Mat(new Size(1, 1), MatType.CV_8UC3, Scalar.Black);
        return handler.DescribeDecision(new FrameContext(screenshot));
    }

    private static object GetRegisteredHandler(RaceRunner runner, string typeName)
    {
        FieldInfo field = typeof(RaceRunner).GetField("_handlers", BindingFlags.Instance | BindingFlags.NonPublic)
                          ?? throw new Xunit.Sdk.XunitException("RaceRunner._handlers was not found.");

        var handlers = (IEnumerable<object>)field.GetValue(runner)!;
        return handlers.Single(h => h.GetType().Name == typeName);
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                          ?? throw new Xunit.Sdk.XunitException($"{target.GetType().Name}.{fieldName} was not found.");
        return field.GetValue(target)
               ?? throw new Xunit.Sdk.XunitException($"{target.GetType().Name}.{fieldName} was null.");
    }

    private static object CreateTradeStateStore()
    {
        Type type = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradeStateStore, SleepRunner")
                    ?? throw new Xunit.Sdk.XunitException("TradeStateStore type was not found.");
        return Activator.CreateInstance(type)
               ?? throw new Xunit.Sdk.XunitException("TradeStateStore could not be created.");
    }

    private static TradePurchaseHandler CreateTradePurchaseHandler(ITradeFlowExecutor executor, object stateStore)
    {
        var constructor = typeof(TradePurchaseHandler).GetConstructor(
                              BindingFlags.Instance | BindingFlags.NonPublic,
                              null,
                              [typeof(ITradeFlowExecutor), stateStore.GetType()],
                              null)
                          ?? throw new Xunit.Sdk.XunitException("TradePurchaseHandler test constructor was not found.");
        return (TradePurchaseHandler)constructor.Invoke([executor, stateStore]);
    }

    private static bool InvokeLoadVisited(object stateStore)
    {
        MethodInfo method = stateStore.GetType().GetMethod(
                                "LoadVisited",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradeStateStore.LoadVisited was not found.");
        return (bool)method.Invoke(stateStore, [])!;
    }

    private static void InvokeSaveVisited(object stateStore, bool visited)
    {
        MethodInfo method = stateStore.GetType().GetMethod(
                                "SaveVisited",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradeStateStore.SaveVisited was not found.");
        method.Invoke(stateStore, [visited]);
    }

    private static void ClearPersistedState()
    {
        InvokeSaveVisited(CreateTradeStateStore(), false);
    }

    private sealed class StaticTradeExecutor : ITradeFlowExecutor
    {
        private readonly TradeExecutionResult _result;

        public StaticTradeExecutor(TradeExecutionResult result)
        {
            _result = result;
        }

        public Task<TradeExecutionResult> ExecuteAsync(GameContext ctx)
        {
            return Task.FromResult(_result);
        }
    }
}
