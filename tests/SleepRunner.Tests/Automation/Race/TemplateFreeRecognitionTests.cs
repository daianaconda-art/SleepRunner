using System.Reflection;
using OpenCvSharp;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class TemplateFreeRecognitionTests
{
    [Fact]
    public void BattleAutoToggle_detects_gray_auto_region_as_off_without_template()
    {
        using var screenshot = CreateScreenshotWithRegion(
            regionX: 0.78,
            regionY: 0.00,
            regionW: 0.10,
            regionH: 0.10,
            color: new Scalar(150, 150, 150));

        string state = InvokeDetectAutoState(screenshot, out double satMean, out double valMean);

        Assert.Equal("OffGray", state);
        Assert.True(satMean < 35, $"Expected low saturation, got {satMean:F1}.");
        Assert.InRange(valMean, 80, 220);
    }

    [Fact]
    public void TrainingFailRateOcr_detects_selected_row_from_red_marker_without_template()
    {
        using var screenshot = CreateScreenshotWithRegion(
            regionX: 0.82,
            regionY: 0.46,
            regionW: 0.10,
            regionH: 0.035,
            color: new Scalar(0, 0, 255));

        int selected = InvokeDetectSelectedOption(screenshot);

        Assert.Equal(2, selected);
    }

    [Theory]
    [InlineData("--prepare")]
    [InlineData("--crop")]
    [InlineData("--click-template")]
    public void Default_cli_does_not_register_template_asset_commands(string commandName)
    {
        Type dispatcherType = Type.GetType("SleepRunner.Cli.CliDispatcher, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("CliDispatcher type was not found.");
        MethodInfo createDefault = dispatcherType.GetMethod(
                                       "CreateDefault",
                                       BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                   ?? throw new Xunit.Sdk.XunitException("CliDispatcher.CreateDefault was not found.");
        object dispatcher = createDefault.Invoke(null, [])!;
        MethodInfo tryResolve = dispatcherType.GetMethod(
                                    "TryResolve",
                                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                ?? throw new Xunit.Sdk.XunitException("CliDispatcher.TryResolve was not found.");

        object?[] args = [new[] { commandName }, null];
        bool resolved = (bool)tryResolve.Invoke(dispatcher, args)!;

        Assert.False(resolved);
    }

    [Fact]
    public void Source_does_not_reference_template_asset_paths()
    {
        string repoRoot = FindRepoRoot();
        string srcRoot = Path.Combine(repoRoot, "src");
        var offenders = Directory
            .EnumerateFiles(srcRoot, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) ||
                           path.EndsWith(".csproj", StringComparison.Ordinal))
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .Where(item =>
                item.Text.Contains("assets/templates", StringComparison.OrdinalIgnoreCase) ||
                item.Text.Contains(@"assets\templates", StringComparison.OrdinalIgnoreCase) ||
                item.Text.Contains("TemplateCropper", StringComparison.Ordinal) ||
                item.Text.Contains("ClickTemplateCommand", StringComparison.Ordinal))
            .Select(item => Path.GetRelativePath(repoRoot, item.Path))
            .OrderBy(path => path)
            .ToArray();

        Assert.Empty(offenders);
    }

    private static string InvokeDetectAutoState(Mat screenshot, out double satMean, out double valMean)
    {
        Type toggleType = Type.GetType("SleepRunner.Automation.Race.Handlers.Battle.BattleAutoToggle, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("BattleAutoToggle type was not found.");
        object toggle = Activator.CreateInstance(toggleType, nonPublic: true)
            ?? throw new Xunit.Sdk.XunitException("BattleAutoToggle could not be created.");
        MethodInfo method = toggleType.GetMethod(
                                "DetectAutoState",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("BattleAutoToggle.DetectAutoState was not found.");

        object?[] args = [screenshot, 0d, 0d, null, 0d];
        object result = method.Invoke(toggle, args)!;
        satMean = (double)args[1]!;
        valMean = (double)args[2]!;
        return result.ToString()!;
    }

    private static int InvokeDetectSelectedOption(Mat screenshot)
    {
        Type ocrType = Type.GetType("SleepRunner.Automation.Race.Handlers.Training.TrainingFailRateOcr, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TrainingFailRateOcr type was not found.");
        MethodInfo method = ocrType.GetMethod(
                                "DetectSelectedOption",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TrainingFailRateOcr.DetectSelectedOption was not found.");

        return (int)method.Invoke(null, [screenshot, null])!;
    }

    private static Mat CreateScreenshotWithRegion(
        double regionX,
        double regionY,
        double regionW,
        double regionH,
        Scalar color)
    {
        const int width = 1000;
        const int height = 1000;
        var screenshot = new Mat(new Size(width, height), MatType.CV_8UC3, new Scalar(18, 18, 18));
        var rect = new Rect(
            (int)(width * regionX),
            (int)(height * regionY),
            Math.Max(1, (int)(width * regionW)),
            Math.Max(1, (int)(height * regionH)));
        Cv2.Rectangle(screenshot, rect, color, thickness: -1);
        return screenshot;
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SleepRunner.sln")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new Xunit.Sdk.XunitException("Repository root was not found.");
    }
}
