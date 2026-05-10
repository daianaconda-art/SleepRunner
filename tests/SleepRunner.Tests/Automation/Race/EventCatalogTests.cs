using System.Reflection;
using SleepRunner.Automation.Race.Policy;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class EventCatalogTests
{
    [Fact]
    public void FindMatchingEvent_does_not_match_result_page_by_stale_title()
    {
        string originalEventsProfile = RaceProfileManager.CurrentEventsProfile;

        try
        {
            RaceProfileManager.SetEventsProfile("speed");
            object catalog = CreateEventCatalog();
            InvokeEnsureLoaded(catalog);

            object? match = InvokeFindMatchingEvent(
                catalog,
                "\u88ad\u51fb",
                "\u9632\u62a4\u63d0\u5347\u4e86\u3002",
                "SPACEBAR");

            Assert.Null(match);
        }
        finally
        {
            RaceProfileManager.SetEventsProfile(originalEventsProfile);
        }
    }

    [Fact]
    public void FindMatchingEvent_matches_blizzard_title_as_snow_before_option_keyword_fallback()
    {
        string originalEventsProfile = RaceProfileManager.CurrentEventsProfile;

        try
        {
            RaceProfileManager.SetEventsProfile("speed");
            object catalog = CreateEventCatalog();
            InvokeEnsureLoaded(catalog);

            object? match = InvokeFindMatchingEvent(
                catalog,
                "\u4eca\u65e5\u5929\u6c14\u4e00\u66b4\u96ea",
                "\u4f7f\u7528\u6696\u6696\u5305\u3002+\u60f3\u529e\u6cd5\u5fcd\u8010\u4e00\u4e0b\u3002",
                "\u5bd2\u6c14\u4ecd\u4e45\u4e45\u4e0d\u6563\u3002");

            Assert.NotNull(match);
            Assert.Equal("weather_snow", GetStringProperty(match, "Id"));
            Assert.Equal(2, GetNullableIntProperty(match, "FallbackOption"));
        }
        finally
        {
            RaceProfileManager.SetEventsProfile(originalEventsProfile);
        }
    }

    [Fact]
    public void VolleyballSemifinal_tries_option_one_then_falls_back_to_option_two()
    {
        string originalEventsProfile = RaceProfileManager.CurrentEventsProfile;

        try
        {
            RaceProfileManager.SetEventsProfile("speed");
            object catalog = CreateEventCatalog();
            InvokeEnsureLoaded(catalog);

            object? match = InvokeFindMatchingEvent(
                catalog,
                "\u6c99\u6ee9\u6392\u7403\u6bd4\u8d5b\u51c6\u51b3\u8d5b",
                "\u5148\u586b\u9971\u809a\u5b50\u518d\u8bf4\u5427\u3002\u7528\u6df1\u547c\u5438\u653e\u677e\u7d27\u5f20\u611f\u5427\u3002",
                "\u6c99\u6ee9\u6392\u7403\u51c6\u51b3\u8d5b\u5373\u5c06\u5f00\u59cb\u3002");

            Assert.NotNull(match);
            Assert.Equal("volleyball_semifinal", GetStringProperty(match, "Id"));
            Assert.Equal(1, GetNullableIntProperty(match, "RecommendedOption"));
            Assert.Equal(2, GetNullableIntProperty(match, "FallbackOption"));
        }
        finally
        {
            RaceProfileManager.SetEventsProfile(originalEventsProfile);
        }
    }

    [Fact]
    public void FindMatchingEvent_matches_maze_exploration_new_localization()
    {
        string originalEventsProfile = RaceProfileManager.CurrentEventsProfile;

        try
        {
            RaceProfileManager.SetEventsProfile("attack");
            object catalog = CreateEventCatalog();
            InvokeEnsureLoaded(catalog);

            object? match = InvokeFindMatchingEvent(
                catalog,
                "\u8ff7\u5bab\u63a2\u52d8",
                "+\u8fd9\u4e2a\u91d1\u989d\u591f\u5417\uff1f\u81ea\u5df1\u5f00\u8d77\u91cd\u673a\u600e\u4e48\u6837\uff1f+\u5f97\u627e\u627e\u770b\u6709\u6ca1\u6709\u66ff\u4ee3\u8def\u7ebf\u4e86\u3002",
                "\u8981\u662f\u7b97\u4e0a\u5371\u9669\u6d25\u8d34\uff0c\u5f00\u4ef7\u53ef\u5f97\u6bd4\u5e73\u5e38\u9ad8\u4e0d\u5c11\u3002");

            Assert.NotNull(match);
            Assert.Equal("maze_exploration", GetStringProperty(match, "Id"));
            Assert.Equal(1, GetNullableIntProperty(match, "RecommendedOption"));
        }
        finally
        {
            RaceProfileManager.SetEventsProfile(originalEventsProfile);
        }
    }

    [Fact]
    public void FindMatchingEvent_matches_thunder_new_localization()
    {
        string originalEventsProfile = RaceProfileManager.CurrentEventsProfile;

        try
        {
            RaceProfileManager.SetEventsProfile("attack");
            object catalog = CreateEventCatalog();
            InvokeEnsureLoaded(catalog);

            object? match = InvokeFindMatchingEvent(
                catalog,
                "\u4eca\u65e5\u5929\u6c14\u4e00\u6253\u96f7",
                "+\u533a\u533a\u95ea\u7535\u6ca1\u4ec0\u4e48\u597d\u6015\u7684\uff01+\u4e0d\u8981\u901e\u5f3a\uff0c\u8fd8\u662f\u5148\u64a4\u79bb\u5427\u3002",
                "\u8fd9\u79cd\u65f6\u5019\u2026");

            Assert.NotNull(match);
            Assert.Equal("weather_thunder", GetStringProperty(match, "Id"));
            Assert.Equal(1, GetNullableIntProperty(match, "RecommendedOption"));
        }
        finally
        {
            RaceProfileManager.SetEventsProfile(originalEventsProfile);
        }
    }

    [Theory]
    [InlineData("attack")]
    [InlineData("default")]
    [InlineData("speed")]
    [InlineData("survival")]
    public void FindMatchingEvent_matches_reset_vacation_marshmallow_localization(string profile)
    {
        string originalEventsProfile = RaceProfileManager.CurrentEventsProfile;

        try
        {
            RaceProfileManager.SetEventsProfile(profile);
            object catalog = CreateEventCatalog();
            InvokeEnsureLoaded(catalog);

            object? match = InvokeFindMatchingEvent(
                catalog,
                "\u8389\u8d5b\u7279\u7684\u5047\u671f",
                "\u8fb9\u627e\u8fb9\u901b\u7684\u65f6\u95f4\u4e5f\u4f1a\u5f88\u6709\u8da3\u7684\u3002\u8fd9\u9644\u8fd1\u5c31\u6709\u3002",
                "\u771f\u7684\u5417\uff1f\u5ba2\u4eba\u4f60\u77e5\u9053\u54ea\u91cc\u6709\u5728\u5356\u68c9\u82b1\u7cd6\u5417\uff1f");

            Assert.NotNull(match);
            Assert.Equal("reset_vacation", GetStringProperty(match, "Id"));
            Assert.Equal(1, GetNullableIntProperty(match, "RecommendedOption"));
        }
        finally
        {
            RaceProfileManager.SetEventsProfile(originalEventsProfile);
        }
    }

    private static object CreateEventCatalog()
    {
        Type catalogType = GetEventCatalogType();
        return Activator.CreateInstance(catalogType)
               ?? throw new Xunit.Sdk.XunitException("EventCatalog could not be constructed.");
    }

    private static void InvokeEnsureLoaded(object catalog)
    {
        MethodInfo method = GetEventCatalogType().GetMethod(
                                "EnsureLoaded",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("EventCatalog.EnsureLoaded was not found.");

        method.Invoke(catalog, null);
    }

    private static object? InvokeFindMatchingEvent(object catalog, string title, string options, string story)
    {
        MethodInfo method = GetEventCatalogType().GetMethod(
                                "FindMatchingEvent",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("EventCatalog.FindMatchingEvent was not found.");

        return method.Invoke(catalog, [title, options, story]);
    }

    private static string GetStringProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName)
                                ?? throw new Xunit.Sdk.XunitException($"{propertyName} property was not found.");

        return (string?)property.GetValue(target) ?? "";
    }

    private static int? GetNullableIntProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName)
                                ?? throw new Xunit.Sdk.XunitException($"{propertyName} property was not found.");

        return (int?)property.GetValue(target);
    }

    private static Type GetEventCatalogType()
    {
        return Type.GetType("SleepRunner.Automation.Race.Handlers.Events.EventCatalog, SleepRunner")
               ?? throw new Xunit.Sdk.XunitException("EventCatalog type was not found.");
    }
}
