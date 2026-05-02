namespace SleepRunner.Input;

public interface IKeyboardInputDevice
{
    Task<bool> EnsureWindowFocusedAsync(IntPtr hWnd, CancellationToken cancellationToken);

    Task<bool> IsWindowFocusedAsync(IntPtr hWnd, CancellationToken cancellationToken);

    Task<bool> SendKeyDownAsync(ushort virtualKey, CancellationToken cancellationToken);

    Task<bool> SendKeyUpAsync(ushort virtualKey, CancellationToken cancellationToken);
}
