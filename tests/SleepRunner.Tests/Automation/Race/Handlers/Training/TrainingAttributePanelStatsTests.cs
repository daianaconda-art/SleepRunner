using System.Reflection;
using OpenCvSharp;
using Xunit;

namespace SleepRunner.Tests.Automation.Race.Handlers.Training;

public class TrainingAttributePanelStatsTests
{
    [Fact]
    public async Task ReadAttributePanelStatsAsync_reads_all_fixed_left_panel_attributes_from_main_menu_fixture()
    {
        string fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "attribute_panel_main_menu.png");
        using var shot = Cv2.ImRead(fixturePath);

        object stats = await InvokeReadAttributePanelStatsAsync(shot);

        Assert.Equal(1250, GetIntProperty(stats, "Strength"));
        Assert.Equal(450, GetIntProperty(stats, "Stamina"));
        Assert.Equal(628, GetIntProperty(stats, "Agility"));
        Assert.Equal(195, GetIntProperty(stats, "Focus"));
        Assert.Equal(119, GetIntProperty(stats, "Guard"));
        Assert.Equal(2373, GetIntProperty(stats, "PotentialPoints"));
    }

    [Fact]
    public async Task ReadAttributePanelStatsAsync_uses_full_panel_context_when_row_ocr_truncates_digits()
    {
        string fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "training_panel_499_348_363.png");
        using var shot = Cv2.ImRead(fixturePath);

        object stats = await InvokeReadAttributePanelStatsAsync(shot);

        Assert.Equal(499, GetIntProperty(stats, "Strength"));
        Assert.Equal(348, GetIntProperty(stats, "Stamina"));
        Assert.Equal(363, GetIntProperty(stats, "Agility"));
        Assert.Equal(70, GetIntProperty(stats, "Focus"));
        Assert.Equal(43, GetIntProperty(stats, "Guard"));
        Assert.Equal(1123, GetIntProperty(stats, "PotentialPoints"));
    }

    [Fact]
    public void ShouldAcceptAttributePanelPixelValue_overrides_same_width_ocr_digit_misread()
    {
        bool accepted = InvokeShouldAcceptAttributePanelPixelValue(ocrValue: 313, pixelValue: 373, pixelConfidence: 0.914);

        Assert.True(accepted);
    }

    [Fact]
    public void ShouldAcceptAttributePanelPixelValue_rejects_different_width_denominator_candidate()
    {
        bool accepted = InvokeShouldAcceptAttributePanelPixelValue(ocrValue: 1250, pixelValue: 4, pixelConfidence: 1.0);

        Assert.False(accepted);
    }

    private static async Task<object> InvokeReadAttributePanelStatsAsync(Mat shot)
    {
        Type type = Type.GetType("SleepRunner.Automation.Race.Handlers.Training.TrainingPowerStat, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TrainingPowerStat type was not found.");

        MethodInfo method = type.GetMethod(
                                "ReadAttributePanelStatsAsync",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TrainingPowerStat.ReadAttributePanelStatsAsync was not found.");

        object? result = method.Invoke(null, [shot]);
        if (result is not Task task)
            throw new Xunit.Sdk.XunitException("ReadAttributePanelStatsAsync did not return a Task.");

        await task;
        PropertyInfo resultProperty = task.GetType().GetProperty("Result")
            ?? throw new Xunit.Sdk.XunitException("ReadAttributePanelStatsAsync task has no Result property.");
        return resultProperty.GetValue(task)
            ?? throw new Xunit.Sdk.XunitException("ReadAttributePanelStatsAsync returned null.");
    }

    private static int? GetIntProperty(object source, string propertyName)
    {
        PropertyInfo property = source.GetType().GetProperty(propertyName)
            ?? throw new Xunit.Sdk.XunitException($"Property {propertyName} was not found.");
        return (int?)property.GetValue(source);
    }

    private static bool InvokeShouldAcceptAttributePanelPixelValue(int? ocrValue, int pixelValue, double pixelConfidence)
    {
        Type type = Type.GetType("SleepRunner.Automation.Race.Handlers.Training.TrainingPowerStat, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TrainingPowerStat type was not found.");

        MethodInfo method = type.GetMethod(
                                "ShouldAcceptAttributePanelPixelValue",
                                BindingFlags.Static | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TrainingPowerStat.ShouldAcceptAttributePanelPixelValue was not found.");

        return Assert.IsType<bool>(method.Invoke(null, [ocrValue, pixelValue, pixelConfidence]));
    }
}
