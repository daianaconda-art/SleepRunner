using SleepRunner.Automation.Race.Policy;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class RaceUserPolicyCardProfileTests
{
    [Fact]
    public void Card_profiles_share_the_same_special_card_whitelist()
    {
        string originalCardsProfile = RaceProfileManager.CurrentCardsProfile;
        string[] profiles = ["default", "attack", "crit_first", "crit_damage_first", "survival"];

        try
        {
            RaceProfileManager.SetCardsProfile("default");
            RaceUserPolicy.ForceReload();
            string[] expectedWhitelistIds = RaceUserPolicy.CardWhitelist.Select(rule => rule.Id).ToArray();
            Assert.NotEmpty(expectedWhitelistIds);

            foreach (string profile in profiles)
            {
                RaceProfileManager.SetCardsProfile(profile);
                RaceUserPolicy.ForceReload();

                string[] actualWhitelistIds = RaceUserPolicy.CardWhitelist.Select(rule => rule.Id).ToArray();

                Assert.Equal(expectedWhitelistIds, actualWhitelistIds);
            }
        }
        finally
        {
            RaceProfileManager.SetCardsProfile(originalCardsProfile);
            RaceUserPolicy.ForceReload();
        }
    }

    [Fact]
    public void CritDamageFirst_profile_prioritizes_crit_damage_before_crit_rate()
    {
        string originalCardsProfile = RaceProfileManager.CurrentCardsProfile;

        try
        {
            RaceProfileManager.SetCardsProfile("crit_damage_first");
            RaceUserPolicy.ForceReload();

            var order = RaceUserPolicy.CardPriorityOrder;

            Assert.True(order.Count >= 2);
            Assert.Equal("\u66b4\u51fb\u4f24\u5bb3", order[0].Label);
            Assert.Contains("\u66b4\u51fb\u4f24\u5bb3", order[0].Keywords);
            Assert.Contains("\u7206\u4f24", order[0].Keywords);
            Assert.Equal("\u66b4\u51fb\u7387", order[1].Label);
            Assert.Contains("\u66b4\u51fb\u7387", order[1].Keywords);
            Assert.Contains("\u66b4\u7387", order[1].Keywords);
        }
        finally
        {
            RaceProfileManager.SetCardsProfile(originalCardsProfile);
            RaceUserPolicy.ForceReload();
        }
    }

    [Fact]
    public void CritDamageFirst_profile_tolerates_crit_damage_ocr_missing_final_character()
    {
        string originalCardsProfile = RaceProfileManager.CurrentCardsProfile;

        try
        {
            RaceProfileManager.SetCardsProfile("crit_damage_first");
            RaceUserPolicy.ForceReload();

            int critDamageRank = RaceUserPolicy.ResolvePriorityRank("自身的暴击伤》10％");
            int critRateRank = RaceUserPolicy.ResolvePriorityRank("自身的暴击率增加10％");

            Assert.Equal(0, critDamageRank);
            Assert.Equal(1, critRateRank);
        }
        finally
        {
            RaceProfileManager.SetCardsProfile(originalCardsProfile);
            RaceUserPolicy.ForceReload();
        }
    }
}
