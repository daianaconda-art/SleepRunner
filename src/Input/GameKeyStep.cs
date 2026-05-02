namespace SleepRunner.Input;

public enum GameKeyStepKind
{
    KeyDown,
    KeyUp,
    Delay,
}

public readonly record struct GameKeyStep(GameKeyStepKind Kind, ushort VirtualKey, int DelayMs)
{
    public static GameKeyStep KeyDown(ushort virtualKey)
    {
        return new GameKeyStep(GameKeyStepKind.KeyDown, virtualKey, 0);
    }

    public static GameKeyStep KeyUp(ushort virtualKey)
    {
        return new GameKeyStep(GameKeyStepKind.KeyUp, virtualKey, 0);
    }

    public static GameKeyStep Delay(int milliseconds)
    {
        if (milliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(milliseconds));

        return new GameKeyStep(GameKeyStepKind.Delay, 0, milliseconds);
    }
}
