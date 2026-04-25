using System.Text.Json;
using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Policy;

public static class EventProfileSettings
{
    private static readonly object SyncRoot = new();
    private static readonly TimeSpan ReloadInterval = TimeSpan.FromSeconds(2);

    private static bool _loaded;
    private static string _loadedPath = string.Empty;
    private static DateTime _loadedMtimeUtc = DateTime.MinValue;
    private static DateTime _lastCheckUtc = DateTime.MinValue;
    private static int _movePlatformOptionIndex = 1;

    static EventProfileSettings()
    {
        RaceProfileManager.EventsProfileChanged += _ => ForceReload();
    }

    public static int MovePlatformOptionIndex
    {
        get
        {
            EnsureLoaded();
            return _movePlatformOptionIndex;
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
            _movePlatformOptionIndex = 1;
        }
    }

    private static void EnsureLoaded()
    {
        lock (SyncRoot)
        {
            string path = RaceProfileManager.CurrentEventsPath;
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

            _movePlatformOptionIndex = LoadMovePlatformOptionIndex(path);
            _loaded = true;
            _loadedPath = path;
            _loadedMtimeUtc = mtime;
        }
    }

    private static int LoadMovePlatformOptionIndex(string path)
    {
        if (!File.Exists(path))
        {
            return 1;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("move_platform_option_index", out JsonElement optionIndexElement))
            {
                return Math.Clamp(optionIndexElement.GetInt32(), 1, 2);
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"[EventProfileSettings] Failed to load settings from '{path}': {ex.Message}");
        }

        return 1;
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
}
