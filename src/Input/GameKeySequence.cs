namespace SleepRunner.Input;

public sealed class GameKeySequence
{
    private readonly GameKeyStep[] _steps;

    public GameKeySequence(IEnumerable<GameKeyStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        _steps = steps.ToArray();
        if (_steps.Length == 0)
            throw new ArgumentException("A key sequence must contain at least one step.", nameof(steps));
    }

    public IReadOnlyList<GameKeyStep> Steps => _steps;

    public static GameKeySequence AltChordTap(ushort key)
    {
        return HoldModifierAndTap(KeyboardSimulator.VK_LMENU, key);
    }

    public static GameKeySequence HoldModifierAndTap(ushort modifier, ushort key)
    {
        return new GameKeySequence(
        [
            GameKeyStep.KeyDown(modifier),
            GameKeyStep.Delay(50),
            GameKeyStep.KeyDown(key),
            GameKeyStep.Delay(25),
            GameKeyStep.KeyUp(key),
            GameKeyStep.Delay(30),
            GameKeyStep.KeyUp(modifier),
        ]);
    }
}
