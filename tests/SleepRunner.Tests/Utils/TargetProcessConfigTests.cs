using SleepRunner.Utils;
using Xunit;

namespace SleepRunner.Tests.Utils;

public class TargetProcessConfigTests
{
    [Theory]
    [InlineData("TargetApp", "TargetApp")]
    [InlineData("TargetApp.exe", "TargetApp")]
    [InlineData("  TargetApp.exe  ", "TargetApp")]
    public void NormalizeProcessName_trims_and_removes_exe_suffix(string value, string expected)
    {
        Assert.Equal(expected, TargetProcessConfig.NormalizeProcessName(value));
    }

    [Fact]
    public void NormalizeProcessName_returns_empty_for_blank_values()
    {
        Assert.Equal("", TargetProcessConfig.NormalizeProcessName("   "));
    }
}
