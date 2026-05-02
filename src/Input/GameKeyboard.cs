using SleepRunner.Utils;

namespace SleepRunner.Input;

public sealed class GameKeyboard
{
    private readonly GameKeymap _keymap;
    private readonly IKeyboardInputDevice _device;
    private readonly Func<int, CancellationToken, Task> _delay;

    public GameKeyboard(GameKeymap keymap)
        : this(keymap, new KeyboardSimulatorInputDevice(), static (milliseconds, token) => Task.Delay(milliseconds, token))
    {
    }

    public GameKeyboard(
        GameKeymap keymap,
        IKeyboardInputDevice device,
        Func<int, CancellationToken, Task>? delay = null)
    {
        _keymap = keymap ?? throw new ArgumentNullException(nameof(keymap));
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _delay = delay ?? (static (milliseconds, token) => Task.Delay(milliseconds, token));
    }

    public static GameKeyboard Default { get; } = new(GameKeymap.Default);

    public async Task<bool> SendActionAsync(
        IntPtr hWnd,
        GameActionKey action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_keymap.TryGetSequence(action, out GameKeySequence? sequence))
        {
            Log.Log($"No key sequence mapped for action {action}.");
            return false;
        }

        GameKeySequence resolvedSequence = sequence
            ?? throw new InvalidOperationException($"No key sequence mapped for action {action}.");

        if (!await _device.EnsureWindowFocusedAsync(hWnd, cancellationToken))
        {
            Log.Log($"{action} key action aborted: target window not focused.");
            return false;
        }

        var pressedKeys = new List<ushort>();
        try
        {
            foreach (GameKeyStep step in resolvedSequence.Steps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (step.Kind)
                {
                    case GameKeyStepKind.KeyDown:
                        if (!await EnsureFocusedBeforeKeyEventAsync(hWnd, action, step, pressedKeys, cancellationToken))
                            return false;
                        if (!await _device.SendKeyDownAsync(step.VirtualKey, cancellationToken))
                        {
                            Log.Log($"{action} key action aborted: key down failed for 0x{step.VirtualKey:X2}.");
                            await ReleasePressedKeysAsync(pressedKeys);
                            return false;
                        }

                        Log.Log($"{action}: key down accepted 0x{step.VirtualKey:X2}.");
                        pressedKeys.Add(step.VirtualKey);
                        break;
                    case GameKeyStepKind.KeyUp:
                        if (!await EnsureFocusedBeforeKeyEventAsync(hWnd, action, step, pressedKeys, cancellationToken))
                            return false;
                        if (!await _device.SendKeyUpAsync(step.VirtualKey, cancellationToken))
                        {
                            Log.Log($"{action} key action aborted: key up failed for 0x{step.VirtualKey:X2}.");
                            await ReleasePressedKeysAsync(pressedKeys);
                            return false;
                        }

                        Log.Log($"{action}: key up accepted 0x{step.VirtualKey:X2}.");
                        RemoveLastPressedKey(pressedKeys, step.VirtualKey);
                        break;
                    case GameKeyStepKind.Delay:
                        await _delay(step.DelayMs, cancellationToken);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported key sequence step '{step.Kind}'.");
                }
            }
        }
        catch
        {
            await ReleasePressedKeysAsync(pressedKeys);
            throw;
        }

        Log.Log($"Game action sent: {action}.");
        return true;
    }

    private async Task<bool> EnsureFocusedBeforeKeyEventAsync(
        IntPtr hWnd,
        GameActionKey action,
        GameKeyStep step,
        List<ushort> pressedKeys,
        CancellationToken cancellationToken)
    {
        if (await _device.IsWindowFocusedAsync(hWnd, cancellationToken))
            return true;

        Log.Log($"{action} key action aborted: target window lost focus before {step.Kind} 0x{step.VirtualKey:X2}.");
        await ReleasePressedKeysAsync(pressedKeys);
        return false;
    }

    private static void RemoveLastPressedKey(List<ushort> pressedKeys, ushort virtualKey)
    {
        for (int i = pressedKeys.Count - 1; i >= 0; i--)
        {
            if (pressedKeys[i] == virtualKey)
            {
                pressedKeys.RemoveAt(i);
                return;
            }
        }
    }

    private async Task ReleasePressedKeysAsync(List<ushort> pressedKeys)
    {
        for (int i = pressedKeys.Count - 1; i >= 0; i--)
        {
            ushort virtualKey = pressedKeys[i];
            try
            {
                bool released = await _device.SendKeyUpAsync(virtualKey, CancellationToken.None);
                if (released)
                    Log.Log($"Released key 0x{virtualKey:X2} after interrupted sequence.");
                else
                    Log.Log($"Failed to release key 0x{virtualKey:X2} after interrupted sequence.");
            }
            catch (Exception ex)
            {
                Log.Log($"Failed to release key 0x{virtualKey:X2} after interrupted sequence: {ex.Message}");
            }
        }

        pressedKeys.Clear();
    }

    private static readonly LogScope Log = new("Keyboard");
}
