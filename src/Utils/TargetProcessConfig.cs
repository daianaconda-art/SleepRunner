namespace SleepRunner.Utils;

public static class TargetProcessConfig
{
    public const string EnvironmentVariableName = "SLEEPRUNNER_TARGET_PROCESS";

    public static string NormalizeProcessName(string? value)
    {
        string trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0)
            return "";

        string fileName = Path.GetFileName(trimmed);
        return fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(fileName)
            : fileName;
    }

    public static bool TryGetConfiguredProcessName(out string processName)
    {
        processName = NormalizeProcessName(Environment.GetEnvironmentVariable(EnvironmentVariableName));
        return processName.Length > 0;
    }

}
