using SleepRunner.Input;
using Xunit;

namespace SleepRunner.Tests.Input;

public class KeyboardSimulatorTests
{
    [Theory]
    [InlineData(KeyboardSimulator.VK_LMENU, 0x38)]
    [InlineData(KeyboardSimulator.VK_1, 0x02)]
    [InlineData(KeyboardSimulator.VK_2, 0x03)]
    [InlineData(KeyboardSimulator.VK_3, 0x04)]
    [InlineData(KeyboardSimulator.VK_SPACE, 0x39)]
    public void GetHardwareScanCode_maps_game_hotkey_keys_to_physical_scan_codes(ushort virtualKey, ushort scanCode)
    {
        Assert.Equal(scanCode, KeyboardSimulator.GetHardwareScanCode(virtualKey));
    }
}
