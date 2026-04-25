using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Policy.Training;

public static class TrainingRuleProfileManager
{
    public static event Action<string>? TrainingProfileChanged;

    public const string DefaultProfileName = "default";

    private static string _currentProfile = DefaultProfileName;

    public static string TrainingDir => Path.Combine(PathHelper.BaseDir, "assets", "training");

    public static string CurrentProfile => _currentProfile;

    public static string CurrentProfilePath => ResolvePath(_currentProfile);

    public static IReadOnlyList<string> ListProfiles()
    {
        try
        {
            if (!Directory.Exists(TrainingDir))
            {
                return Array.Empty<string>();
            }

            return Directory.EnumerateFiles(TrainingDir, "*.json", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Logger.Log($"[TrainingRuleProfileManager] ListProfiles failed: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    public static string ResolvePath(string profileName)
    {
        return Path.Combine(TrainingDir, SanitizeProfile(profileName) + ".json");
    }

    public static void SetCurrentProfile(string profileName)
    {
        string cleaned = SanitizeProfile(profileName);
        if (string.Equals(_currentProfile, cleaned, StringComparison.Ordinal))
        {
            return;
        }

        _currentProfile = cleaned;
        Logger.Log($"[TrainingRuleProfileManager] Training profile -> '{cleaned}' (path={CurrentProfilePath})");
        SafeRaise(TrainingProfileChanged, cleaned);
    }

    private static string SanitizeProfile(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DefaultProfileName;
        }

        string value = raw.Trim();
        foreach (char ch in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(ch.ToString(), string.Empty);
        }

        if (value.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^5];
        }

        return string.IsNullOrWhiteSpace(value) ? DefaultProfileName : value;
    }

    private static void SafeRaise(Action<string>? evt, string payload)
    {
        if (evt == null)
        {
            return;
        }

        foreach (Action<string> subscriber in evt.GetInvocationList().Cast<Action<string>>())
        {
            try
            {
                subscriber(payload);
            }
            catch (Exception ex)
            {
                Logger.Log($"[TrainingRuleProfileManager] Subscriber threw: {ex.Message}");
            }
        }
    }
}
