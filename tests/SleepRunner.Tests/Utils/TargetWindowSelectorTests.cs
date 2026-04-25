using SleepRunner.Utils;
using Xunit;

namespace SleepRunner.Tests.Utils;

public class TargetWindowSelectorTests
{
    [Fact]
    public void SelectBestCandidate_prefers_largest_unity_window()
    {
        var candidates = new[]
        {
            new TargetWindowCandidate(Handle: 1, ProcessId: 100, ClassName: "Chrome_WidgetWin_1", ClientArea: 4_000_000, IsVisible: true),
            new TargetWindowCandidate(Handle: 2, ProcessId: 101, ClassName: "UnityWndClass", ClientArea: 1_000_000, IsVisible: true),
            new TargetWindowCandidate(Handle: 3, ProcessId: 102, ClassName: "UnityWndClass", ClientArea: 2_000_000, IsVisible: true),
        };

        TargetWindowCandidate? selected = TargetWindowSelector.SelectBestCandidate(candidates, currentProcessId: 999);

        Assert.NotNull(selected);
        Assert.Equal(3, selected.Value.Handle);
    }

    [Fact]
    public void SelectBestCandidate_ignores_current_process()
    {
        var candidates = new[]
        {
            new TargetWindowCandidate(Handle: 1, ProcessId: 200, ClassName: "UnityWndClass", ClientArea: 3_000_000, IsVisible: true),
            new TargetWindowCandidate(Handle: 2, ProcessId: 201, ClassName: "UnityWndClass", ClientArea: 2_000_000, IsVisible: true),
        };

        TargetWindowCandidate? selected = TargetWindowSelector.SelectBestCandidate(candidates, currentProcessId: 200);

        Assert.NotNull(selected);
        Assert.Equal(2, selected.Value.Handle);
    }

    [Fact]
    public void SelectBestCandidate_returns_null_when_no_visible_unity_window_exists()
    {
        var candidates = new[]
        {
            new TargetWindowCandidate(Handle: 1, ProcessId: 100, ClassName: "Chrome_WidgetWin_1", ClientArea: 4_000_000, IsVisible: true),
            new TargetWindowCandidate(Handle: 2, ProcessId: 101, ClassName: "UnityWndClass", ClientArea: 1_000_000, IsVisible: false),
        };

        TargetWindowCandidate? selected = TargetWindowSelector.SelectBestCandidate(candidates, currentProcessId: 999);

        Assert.Null(selected);
    }
}
