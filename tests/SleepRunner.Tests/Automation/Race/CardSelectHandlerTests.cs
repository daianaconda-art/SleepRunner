using System.Reflection;
using OpenCvSharp;
using SleepRunner.Automation.Race.Handlers;
using SleepRunner.Automation.Race.Policy;
using SleepRunner.Vision;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class CardSelectHandlerTests
{
    [Fact]
    public void CanHandle_returns_true_when_title_is_only_visible_in_fallback_region()
    {
        using var screenshot = new Mat(new Size(1280, 720), MatType.CV_8UC3, new Scalar(18, 18, 18));
        var handler = new CardSelectHandler(ReadRegion);

        bool canHandle = handler.CanHandle(new FrameContext(screenshot));

        Assert.True(canHandle);

        static string ReadRegion(Mat _, double x, double y, double w, double h)
        {
            return IsRegion(x, y, w, h, 0.01, 0.07, 0.22, 0.12)
                ? "选择奖励"
                : "";
        }
    }

    [Fact]
    public void CanHandle_returns_true_when_reward_marker_is_visible_in_zero_origin_region()
    {
        using var screenshot = new Mat(new Size(1280, 720), MatType.CV_8UC3, new Scalar(18, 18, 18));
        var handler = new CardSelectHandler(ReadRegion);

        bool canHandle = handler.CanHandle(new FrameContext(screenshot));

        Assert.True(canHandle);

        static string ReadRegion(Mat _, double x, double y, double w, double h)
        {
            return IsRegion(x, y, w, h, 0.00, 0.00, 0.26, 0.12)
                ? "．选择奖励"
                : "";
        }
    }

    [Fact]
    public void Priority_order_uses_apocalypse_necklace_name_when_crit_damage_effect_is_ocr_truncated()
    {
        string originalCardsProfile = RaceProfileManager.CurrentCardsProfile;

        try
        {
            RaceProfileManager.SetCardsProfile("crit_damage_first");
            RaceUserPolicy.ForceReload();
            var handler = new CardSelectHandler(ReadRegion);

            int[] order = InvokeBuildPriorityAttemptOrder(
                handler,
                [
                    "启示录新型项炼队员全体首次战斗开始时，自身的害增加5％。",
                    "启示录新型帽子队员全体首次战斗开始时，自身的暴击率增加5％。",
                    "启示录新型铠甲队员全体首次战斗开始时，自身的最大生命力增加4％。",
                ]);

            Assert.Equal([0, 1], order);
        }
        finally
        {
            RaceProfileManager.SetCardsProfile(originalCardsProfile);
            RaceUserPolicy.ForceReload();
        }

        static string ReadRegion(Mat _, double x, double y, double w, double h) => "";
    }

    [Fact]
    public void Red_commission_fei_first_reward_clicks_unselected_when_no_life_leech_totem_candidate()
    {
        string originalCardsProfile = RaceProfileManager.CurrentCardsProfile;

        try
        {
            RaceProfileManager.SetCardsProfile("fei");
            RaceUserPolicy.ForceReload();
            var handler = new CardSelectHandler(ReadRegion);

            bool shouldClickUnselected = InvokeShouldClickUnselectedForRedCommissionFirstReward(
                handler,
                [
                    "\u81ea\u8eab\u7684\u66b4\u51fb\u4f24\u5bb3\u589e\u52a016\uff05",
                    "\u81ea\u8eab\u7684\u653b\u51fb\u529b\u589e\u52a016\uff05",
                    "\u81ea\u8eab\u7684\u66b4\u51fb\u7387\u589e\u52a016\uff05",
                ],
                hasPendingRedCommissionReward: true);

            Assert.True(shouldClickUnselected);
        }
        finally
        {
            RaceProfileManager.SetCardsProfile(originalCardsProfile);
            RaceUserPolicy.ForceReload();
        }

        static string ReadRegion(Mat _, double x, double y, double w, double h) => "";
    }

    [Fact]
    public void Red_commission_fei_first_reward_keeps_normal_selection_when_life_leech_totem_candidate_exists()
    {
        string originalCardsProfile = RaceProfileManager.CurrentCardsProfile;

        try
        {
            RaceProfileManager.SetCardsProfile("fei");
            RaceUserPolicy.ForceReload();
            var handler = new CardSelectHandler(ReadRegion);

            bool shouldClickUnselected = InvokeShouldClickUnselectedForRedCommissionFirstReward(
                handler,
                [
                    "\u81ea\u8eab\u7684\u66b4\u51fb\u4f24\u5bb3\u589e\u52a016\uff05",
                    "\u53e4\u4ee3\u6551\u63f4\u8005\u7684\u56fe\u817e \u961f\u5458\u5168\u4f53 \u9996\u6b21\u6218\u6597\u5f00\u59cb\u65f6\uff0c\u81ea\u8eab\u7684\u751f\u547d\u529b\u5438\u53d6\u7387\u589e\u52a015\uff05",
                    "\u81ea\u8eab\u7684\u653b\u51fb\u529b\u589e\u52a016\uff05",
                ],
                hasPendingRedCommissionReward: true);

            Assert.False(shouldClickUnselected);
        }
        finally
        {
            RaceProfileManager.SetCardsProfile(originalCardsProfile);
            RaceUserPolicy.ForceReload();
        }

        static string ReadRegion(Mat _, double x, double y, double w, double h) => "";
    }

    [Theory]
    [InlineData("fei", false)]
    [InlineData("default", true)]
    public void Red_commission_first_reward_does_not_change_normal_card_selection_without_full_gate(
        string cardsProfile,
        bool hasPendingRedCommissionReward)
    {
        string originalCardsProfile = RaceProfileManager.CurrentCardsProfile;

        try
        {
            RaceProfileManager.SetCardsProfile(cardsProfile);
            RaceUserPolicy.ForceReload();
            var handler = new CardSelectHandler(ReadRegion);

            bool shouldClickUnselected = InvokeShouldClickUnselectedForRedCommissionFirstReward(
                handler,
                [
                    "\u81ea\u8eab\u7684\u66b4\u51fb\u4f24\u5bb3\u589e\u52a016\uff05",
                    "\u81ea\u8eab\u7684\u653b\u51fb\u529b\u589e\u52a016\uff05",
                    "\u81ea\u8eab\u7684\u66b4\u51fb\u7387\u589e\u52a016\uff05",
                ],
                hasPendingRedCommissionReward);

            Assert.False(shouldClickUnselected);
        }
        finally
        {
            RaceProfileManager.SetCardsProfile(originalCardsProfile);
            RaceUserPolicy.ForceReload();
        }

        static string ReadRegion(Mat _, double x, double y, double w, double h) => "";
    }

    private static int[] InvokeBuildPriorityAttemptOrder(CardSelectHandler handler, string[] texts)
    {
        MethodInfo method = typeof(CardSelectHandler).GetMethod(
                                "BuildPriorityAttemptOrder",
                                BindingFlags.Instance | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("CardSelectHandler.BuildPriorityAttemptOrder was not found.");

        object? result = method.Invoke(handler, [texts]);
        Assert.NotNull(result);
        return ((System.Collections.IEnumerable)result!)
            .Cast<int>()
            .ToArray();
    }

    private static bool InvokeShouldClickUnselectedForRedCommissionFirstReward(
        CardSelectHandler handler,
        string[] texts,
        bool hasPendingRedCommissionReward)
    {
        MethodInfo method = typeof(CardSelectHandler).GetMethod(
                                "ShouldClickUnselectedForRedCommissionFirstReward",
                                BindingFlags.Instance | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("CardSelectHandler.ShouldClickUnselectedForRedCommissionFirstReward was not found.");

        return (bool)method.Invoke(handler, [texts, hasPendingRedCommissionReward])!;
    }

    private static bool IsRegion(
        double actualX,
        double actualY,
        double actualW,
        double actualH,
        double expectedX,
        double expectedY,
        double expectedW,
        double expectedH)
    {
        const double epsilon = 0.0001;
        return Math.Abs(actualX - expectedX) < epsilon &&
               Math.Abs(actualY - expectedY) < epsilon &&
               Math.Abs(actualW - expectedW) < epsilon &&
               Math.Abs(actualH - expectedH) < epsilon;
    }
}
