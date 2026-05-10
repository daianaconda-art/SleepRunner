using SleepRunner.Input;
using Xunit;

namespace SleepRunner.Tests.Input;

public class GameKeymapTests
{
    [Fact]
    public void Default_trade_purchase_holds_alt_and_taps_space()
    {
        GameKeySequence sequence = GameKeymap.Default.GetSequence(GameActionKey.TradePurchase);

        AssertAltChordTap(sequence, KeyboardSimulator.VK_SPACE);
    }

    [Fact]
    public void Default_appraise_accept_holds_alt_and_taps_space()
    {
        var action = Enum.Parse<GameActionKey>("AppraiseAccept");

        GameKeySequence sequence = GameKeymap.Default.GetSequence(action);

        AssertAltChordTap(sequence, KeyboardSimulator.VK_SPACE);
    }

    [Theory]
    [InlineData("TradeSelectSlot1", KeyboardSimulator.VK_1)]
    [InlineData("TradeSelectSlot2", KeyboardSimulator.VK_2)]
    [InlineData("TradeSelectSlot3", KeyboardSimulator.VK_3)]
    [InlineData("RestSelectOption1", KeyboardSimulator.VK_1)]
    [InlineData("RestSelectOption2", KeyboardSimulator.VK_2)]
    [InlineData("RestSelectOption3", KeyboardSimulator.VK_3)]
    public void Default_trade_slot_selection_uses_alt_chord_number_keys(string actionName, ushort key)
    {
        var action = Enum.Parse<GameActionKey>(actionName);

        GameKeySequence sequence = GameKeymap.Default.GetSequence(action);

        AssertAltChordTap(sequence, key);
    }

    [Theory]
    [InlineData("EventSelectOption1", KeyboardSimulator.VK_1)]
    [InlineData("EventSelectOption2", KeyboardSimulator.VK_2)]
    [InlineData("EventSelectOption3", KeyboardSimulator.VK_3)]
    [InlineData("EventSelectOption4", KeyboardSimulator.VK_4)]
    public void Default_event_option_selection_uses_alt_chord_number_keys(string actionName, ushort key)
    {
        var action = Enum.Parse<GameActionKey>(actionName);

        GameKeySequence sequence = GameKeymap.Default.GetSequence(action);

        AssertAltChordTap(sequence, key);
    }

    [Fact]
    public void Default_main_menu_rest_uses_alt_chord_three()
    {
        GameKeySequence sequence = GameKeymap.Default.GetSequence(GameActionKey.MainMenuRest);

        AssertAltChordTap(sequence, KeyboardSimulator.VK_3);
    }

    [Fact]
    public void AltChordTap_holds_alt_and_taps_the_target_key()
    {
        GameKeySequence sequence = GameKeySequence.AltChordTap(KeyboardSimulator.VK_SPACE);

        AssertAltChordTap(sequence, KeyboardSimulator.VK_SPACE);
    }

    private static void AssertAltChordTap(GameKeySequence sequence, ushort key)
    {
        Assert.Collection(
            sequence.Steps,
            step =>
            {
                Assert.Equal(GameKeyStepKind.KeyDown, step.Kind);
                Assert.Equal(KeyboardSimulator.VK_LMENU, step.VirtualKey);
            },
            step =>
            {
                Assert.Equal(GameKeyStepKind.Delay, step.Kind);
                Assert.Equal(50, step.DelayMs);
            },
            step =>
            {
                Assert.Equal(GameKeyStepKind.KeyDown, step.Kind);
                Assert.Equal(key, step.VirtualKey);
            },
            step =>
            {
                Assert.Equal(GameKeyStepKind.Delay, step.Kind);
                Assert.Equal(25, step.DelayMs);
            },
            step =>
            {
                Assert.Equal(GameKeyStepKind.KeyUp, step.Kind);
                Assert.Equal(key, step.VirtualKey);
            },
            step =>
            {
                Assert.Equal(GameKeyStepKind.Delay, step.Kind);
                Assert.Equal(30, step.DelayMs);
            },
            step =>
            {
                Assert.Equal(GameKeyStepKind.KeyUp, step.Kind);
                Assert.Equal(KeyboardSimulator.VK_LMENU, step.VirtualKey);
            });
    }
}
