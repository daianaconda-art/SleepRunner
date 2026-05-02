using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Policy.Training;

public static class TrainingRuleStore
{
    private static readonly object SyncRoot = new();
    private static readonly TimeSpan ReloadInterval = TimeSpan.FromSeconds(2);
    private static readonly TrainingRuleProfile BuiltinDefaultProfile = CreateBuiltinDefaultProfile();

    private static bool _loaded;
    private static string _loadedPath = string.Empty;
    private static DateTime _loadedMtimeUtc = DateTime.MinValue;
    private static DateTime _lastCheckUtc = DateTime.MinValue;
    private static TrainingRuleProfile _currentProfile = BuiltinDefaultProfile;

    static TrainingRuleStore()
    {
        TrainingRuleProfileManager.TrainingProfileChanged += _ => ForceReload();
    }

    public static TrainingRuleProfile CurrentProfile
    {
        get
        {
            EnsureLoaded();
            return CloneProfile(_currentProfile);
        }
    }

    public static TrainingRuleProfile LoadFromPath(string path)
    {
        if (!File.Exists(path))
        {
            Logger.Log($"[TrainingRuleStore] Profile not found, using builtin default: {path}");
            return CloneBuiltinDefault();
        }

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                Logger.Log($"[TrainingRuleStore] Profile empty, using builtin default: {path}");
                return CloneBuiltinDefault();
            }

            return TrainingRuleLoader.LoadFromJson(json, path);
        }
        catch (Exception ex)
        {
            Logger.Log($"[TrainingRuleStore] Profile load failed ({path}): {ex.Message}, using builtin default");
            return CloneBuiltinDefault();
        }
    }

    public static void ForceReload()
    {
        lock (SyncRoot)
        {
            _loaded = false;
            _loadedPath = string.Empty;
            _loadedMtimeUtc = DateTime.MinValue;
            _lastCheckUtc = DateTime.MinValue;
        }
    }

    private static void EnsureLoaded()
    {
        lock (SyncRoot)
        {
            string path = TrainingRuleProfileManager.CurrentProfilePath;
            DateTime now = DateTime.UtcNow;
            bool pathChanged = !string.Equals(_loadedPath, path, StringComparison.OrdinalIgnoreCase);

            if (_loaded && !pathChanged && now - _lastCheckUtc < ReloadInterval)
            {
                return;
            }

            _lastCheckUtc = now;

            DateTime mtime = SafeGetMtime(path);
            if (_loaded && !pathChanged && mtime == _loadedMtimeUtc)
            {
                return;
            }

            _currentProfile = LoadFromPath(path);
            _loaded = true;
            _loadedPath = path;
            _loadedMtimeUtc = mtime;
        }
    }

    private static DateTime SafeGetMtime(string path)
    {
        try
        {
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static TrainingRuleProfile CreateBuiltinDefaultProfile()
    {
        var profile = new TrainingRuleProfile
        {
            SourcePath = "builtin_default",
        };
        profile.Rules.Add(
            new TrainingRuleCard
            {
                Id = "builtin_default",
                Action = TrainingDecisionAction.BuiltinDefault,
                Enabled = true,
                IsFallback = true,
            });
        return profile;
    }

    private static TrainingRuleProfile CloneBuiltinDefault()
    {
        return CloneProfile(BuiltinDefaultProfile);
    }

    private static TrainingRuleProfile CloneProfile(TrainingRuleProfile source)
    {
        var clone = new TrainingRuleProfile
        {
            SourcePath = source.SourcePath,
        };

        clone.LegacyStrategy.BuildDirection = source.LegacyStrategy.BuildDirection;
        clone.LegacyStrategy.FailRateThreshold = source.LegacyStrategy.FailRateThreshold;
        clone.LegacyStrategy.RushThreshold = source.LegacyStrategy.RushThreshold;

        foreach (var rule in source.Rules)
        {
            var ruleClone = new TrainingRuleCard
            {
                Id = rule.Id,
                Field = rule.Field,
                Operator = rule.Operator,
                Value = rule.Value,
                Action = rule.Action,
                Enabled = rule.Enabled,
                IsFallback = rule.IsFallback,
            };

            foreach (TrainingRuleCondition condition in rule.Conditions)
            {
                ruleClone.Conditions.Add(new TrainingRuleCondition
                {
                    Field = condition.Field,
                    Operator = condition.Operator,
                    Value = condition.Value,
                });
            }

            clone.Rules.Add(ruleClone);
        }

        return clone;
    }
}
