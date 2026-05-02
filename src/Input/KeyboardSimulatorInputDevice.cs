namespace SleepRunner.Input;

internal sealed class KeyboardSimulatorInputDevice : IKeyboardInputDevice
{
    public async Task<bool> EnsureWindowFocusedAsync(IntPtr hWnd, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool focused = await KeyboardSimulator.EnsureWindowFocused(hWnd);
        cancellationToken.ThrowIfCancellationRequested();
        return focused;
    }

    public Task<bool> IsWindowFocusedAsync(IntPtr hWnd, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(KeyboardSimulator.IsWindowFocused(hWnd));
    }

    public Task<bool> SendKeyDownAsync(ushort virtualKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(KeyboardSimulator.SendKeyDown(virtualKey));
    }

    public Task<bool> SendKeyUpAsync(ushort virtualKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(KeyboardSimulator.SendKeyUp(virtualKey));
    }
}
