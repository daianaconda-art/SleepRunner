using SleepRunner.Input;
using Xunit;

namespace SleepRunner.Tests.Input;

public class GameKeyboardTests
{
    [Fact]
    public async Task SendActionAsync_focuses_window_then_sends_trade_purchase_sequence()
    {
        var device = new RecordingKeyboardInputDevice(focusResult: true);
        var delays = new List<int>();
        var keyboard = new GameKeyboard(
            GameKeymap.Default,
            device,
            (milliseconds, _) =>
            {
                delays.Add(milliseconds);
                return Task.CompletedTask;
            });

        bool sent = await keyboard.SendActionAsync(new IntPtr(123), GameActionKey.TradePurchase);

        Assert.True(sent);
        Assert.Equal([new IntPtr(123)], device.FocusRequests);
        Assert.Equal(
            [new IntPtr(123), new IntPtr(123), new IntPtr(123), new IntPtr(123)],
            device.FocusChecks);
        Assert.Equal(
            [
                $"down:0x{KeyboardSimulator.VK_LMENU:X2}",
                $"down:0x{KeyboardSimulator.VK_SPACE:X2}",
                $"up:0x{KeyboardSimulator.VK_SPACE:X2}",
                $"up:0x{KeyboardSimulator.VK_LMENU:X2}"
            ],
            device.KeyEvents);
        Assert.Equal([50, 25, 30], delays);
    }

    [Fact]
    public async Task SendActionAsync_aborts_when_target_window_cannot_be_focused()
    {
        var device = new RecordingKeyboardInputDevice(focusResult: false);
        var keyboard = new GameKeyboard(
            GameKeymap.Default,
            device,
            (_, _) => throw new Xunit.Sdk.XunitException("Delay should not run when focus fails."));

        bool sent = await keyboard.SendActionAsync(new IntPtr(456), GameActionKey.TradePurchase);

        Assert.False(sent);
        Assert.Equal([new IntPtr(456)], device.FocusRequests);
        Assert.Empty(device.FocusChecks);
        Assert.Empty(device.KeyEvents);
    }

    [Fact]
    public async Task SendActionAsync_releases_pressed_keys_when_sequence_is_interrupted()
    {
        var device = new RecordingKeyboardInputDevice(focusResult: true);
        var keyboard = new GameKeyboard(
            GameKeymap.Default,
            device,
            (_, _) => throw new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => keyboard.SendActionAsync(new IntPtr(789), GameActionKey.TradePurchase));

        Assert.Equal(
            [
                $"down:0x{KeyboardSimulator.VK_LMENU:X2}",
                $"up:0x{KeyboardSimulator.VK_LMENU:X2}"
            ],
            device.KeyEvents);
    }

    [Fact]
    public async Task SendActionAsync_aborts_when_alt_down_is_not_accepted()
    {
        var device = new RecordingKeyboardInputDevice(
            focusResult: true,
            keyDownResults: new Dictionary<ushort, bool>
            {
                [KeyboardSimulator.VK_LMENU] = false,
            });
        var keyboard = new GameKeyboard(
            GameKeymap.Default,
            device,
            (_, _) => throw new Xunit.Sdk.XunitException("Delay should not run when Alt down fails."));

        bool sent = await keyboard.SendActionAsync(new IntPtr(321), GameActionKey.TradePurchase);

        Assert.False(sent);
        Assert.Equal(
            [
                $"down:0x{KeyboardSimulator.VK_LMENU:X2}"
            ],
            device.KeyEvents);
    }

    [Fact]
    public async Task SendActionAsync_aborts_and_releases_alt_when_focus_is_lost_before_space()
    {
        var device = new RecordingKeyboardInputDevice(
            focusResult: true,
            focusChecks: [true, false]);
        var keyboard = new GameKeyboard(
            GameKeymap.Default,
            device,
            (milliseconds, _) => Task.CompletedTask);

        bool sent = await keyboard.SendActionAsync(new IntPtr(654), GameActionKey.TradePurchase);

        Assert.False(sent);
        Assert.Equal(
            [
                $"down:0x{KeyboardSimulator.VK_LMENU:X2}",
                $"up:0x{KeyboardSimulator.VK_LMENU:X2}"
            ],
            device.KeyEvents);
        Assert.Equal([new IntPtr(654), new IntPtr(654)], device.FocusChecks);
    }

    private sealed class RecordingKeyboardInputDevice(
        bool focusResult,
        IReadOnlyList<bool>? focusChecks = null,
        IReadOnlyDictionary<ushort, bool>? keyDownResults = null,
        IReadOnlyDictionary<ushort, bool>? keyUpResults = null) : IKeyboardInputDevice
    {
        public List<IntPtr> FocusRequests { get; } = [];
        public List<IntPtr> FocusChecks { get; } = [];
        public List<string> KeyEvents { get; } = [];
        private int _focusCheckIndex;

        public Task<bool> EnsureWindowFocusedAsync(IntPtr hWnd, CancellationToken cancellationToken)
        {
            FocusRequests.Add(hWnd);
            return Task.FromResult(focusResult);
        }

        public Task<bool> IsWindowFocusedAsync(IntPtr hWnd, CancellationToken cancellationToken)
        {
            FocusChecks.Add(hWnd);
            bool result = focusChecks == null ||
                          _focusCheckIndex >= focusChecks.Count ||
                          focusChecks[_focusCheckIndex];
            _focusCheckIndex++;
            return Task.FromResult(result);
        }

        public Task<bool> SendKeyDownAsync(ushort virtualKey, CancellationToken cancellationToken)
        {
            KeyEvents.Add($"down:0x{virtualKey:X2}");
            return Task.FromResult(GetKeyResult(keyDownResults, virtualKey));
        }

        public Task<bool> SendKeyUpAsync(ushort virtualKey, CancellationToken cancellationToken)
        {
            KeyEvents.Add($"up:0x{virtualKey:X2}");
            return Task.FromResult(GetKeyResult(keyUpResults, virtualKey));
        }

        private static bool GetKeyResult(IReadOnlyDictionary<ushort, bool>? results, ushort virtualKey)
        {
            return results == null ||
                   !results.TryGetValue(virtualKey, out bool result) ||
                   result;
        }
    }
}
