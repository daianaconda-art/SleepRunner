using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class TradePurchaseHandlerTests
{
    [Fact]
    public void LooksLikeTradeListText_rejects_reward_story_text_with_purchase_word()
    {
        string rewardText = "\u0030\u0030\u81ea\u8eab\u7684\u6548\u679c\u547d\u4e2d\u589e\u52a0\u0031\u0030\u6218\u6597\u4e2d\u4ec5\u89e6\u53d1\u0031\u5915\u6caa\u5507\u8d2d\u4e70\u6708\u5149\u971c\u968f\u77fd\u8d08\u3002";

        bool looksLikeTradeList = InvokeLooksLikeTradeListText(rewardText);

        Assert.False(looksLikeTradeList);
    }

    [Fact]
    public void LooksLikeTradeListText_accepts_trade_context_with_prices()
    {
        string tradeText = "\u4ea4\u6613\u5546\u5e97\u0030\u0036\u0030\u0030\u0038\u0030";

        bool looksLikeTradeList = InvokeLooksLikeTradeListText(tradeText);

        Assert.True(looksLikeTradeList);
    }

    [Fact]
    public void LooksLikeTradeListText_rejects_move_platform_shop_flavor_text()
    {
        string movePlatformText = "\u4ee5\u53e4\u4ee3\u9057\u8ff9\u95fb\u540d\u3001\u56f4\u7ed5\u5947\u4fee\u5362\u5e03\u6d1e\u7a9f\u53d1\u5c55\u800c\u6210\u7684\u57ce\u3002\u6b64\u5730\u6316\u6398\u51fa\u5927\u91cf\u5e0c\u6709\u9b54\u5de5\u5b66\u6750\u6599\uff0c\u4ee5\u53ca\u63a8\u6d4b\u6765\u767d\u4ee3\u7acb\u540d\u9977\u8f73\u7b26\u00b7\u5730\u533a\u4ecb\u7ecd\u5728\u97e7\u6027/\u96c6\u4e2d/\u4fdd\u62a4\u8bad\u7ec3\u4e2d\u4f1a\u83b7\u5f97\u52a0\u6210\uff0c\u53ef\u4e8e\u5546\u5e97\u8d2d\u4e70\u5404\u79cd\u7279\u6b8a\u7f8e\u98df\u30025\uff05@+5\uff05";

        bool looksLikeTradeList = InvokeLooksLikeTradeListText(movePlatformText);

        Assert.False(looksLikeTradeList);
    }

    private static bool InvokeLooksLikeTradeListText(string text)
    {
        Type handlerType = Type.GetType("SleepRunner.Automation.Race.Handlers.TradePurchaseHandler, SleepRunner")
                           ?? throw new Xunit.Sdk.XunitException("TradePurchaseHandler type was not found.");
        MethodInfo method = handlerType.GetMethod(
                                "LooksLikeTradeListText",
                                BindingFlags.Static | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradePurchaseHandler.LooksLikeTradeListText was not found.");

        return (bool)method.Invoke(null, [text])!;
    }
}
