namespace SleepRunner.Input;

public sealed class GameKeymap
{
    private readonly IReadOnlyDictionary<GameActionKey, GameKeySequence> _sequences;

    public GameKeymap(IReadOnlyDictionary<GameActionKey, GameKeySequence> sequences)
    {
        ArgumentNullException.ThrowIfNull(sequences);
        _sequences = new Dictionary<GameActionKey, GameKeySequence>(sequences);
    }

    public static GameKeymap Default { get; } = new(new Dictionary<GameActionKey, GameKeySequence>
    {
        [GameActionKey.AppraiseAccept] = GameKeySequence.AltChordTap(KeyboardSimulator.VK_SPACE),
        [GameActionKey.TradePurchase] = GameKeySequence.AltChordTap(KeyboardSimulator.VK_SPACE),
        [GameActionKey.TradeSelectSlot1] = GameKeySequence.AltChordTap(KeyboardSimulator.VK_1),
        [GameActionKey.TradeSelectSlot2] = GameKeySequence.AltChordTap(KeyboardSimulator.VK_2),
        [GameActionKey.TradeSelectSlot3] = GameKeySequence.AltChordTap(KeyboardSimulator.VK_3),
        [GameActionKey.MainMenuRest] = GameKeySequence.AltChordTap(KeyboardSimulator.VK_3),
        [GameActionKey.RestSelectOption1] = GameKeySequence.AltChordTap(KeyboardSimulator.VK_1),
        [GameActionKey.RestSelectOption2] = GameKeySequence.AltChordTap(KeyboardSimulator.VK_2),
        [GameActionKey.RestSelectOption3] = GameKeySequence.AltChordTap(KeyboardSimulator.VK_3),
        [GameActionKey.EventSelectOption1] = GameKeySequence.AltChordTap(KeyboardSimulator.VK_1),
        [GameActionKey.EventSelectOption2] = GameKeySequence.AltChordTap(KeyboardSimulator.VK_2),
        [GameActionKey.EventSelectOption3] = GameKeySequence.AltChordTap(KeyboardSimulator.VK_3),
        [GameActionKey.EventSelectOption4] = GameKeySequence.AltChordTap(KeyboardSimulator.VK_4),
    });

    public GameKeySequence GetSequence(GameActionKey action)
    {
        if (!_sequences.TryGetValue(action, out GameKeySequence? sequence))
            throw new KeyNotFoundException($"No key sequence is mapped for game action '{action}'.");

        return sequence;
    }

    public bool TryGetSequence(GameActionKey action, out GameKeySequence? sequence)
    {
        return _sequences.TryGetValue(action, out sequence);
    }
}
