namespace SleepRunner.Utils;

/// <summary>
/// 统一日志系统，支持同时输出到控制台和 UI
/// </summary>
public static class Logger
{
    private static readonly object SyncRoot = new();
    private static StreamWriter? _sessionWriter;
    private static StreamWriter? _latestWriter;
    private static string? _currentSessionPath;
    private static System.Diagnostics.Stopwatch? _sessionStopwatch;
    private static string? _currentSessionName;

    /// <summary>
    /// UI 订阅此事件来接收日志（已带时间戳的完整行）
    /// </summary>
    public static event Action<string>? OnLog;

    /// <summary>
    /// 当前会话日志文件路径。未启动会话时为空。
    /// </summary>
    public static string? CurrentSessionPath
    {
        get
        {
            lock (SyncRoot)
                return _currentSessionPath;
        }
    }

    /// <summary>
    /// 当前会话名称。未启动会话时为空。
    /// </summary>
    public static string? CurrentSessionName
    {
        get
        {
            lock (SyncRoot)
                return _currentSessionName;
        }
    }

    /// <summary>
    /// 启动一个新的日志会话，可选输出到独立会话文件与 latest.log。
    /// </summary>
    public static void StartSession(
        string sessionName,
        bool writeSessionFile = true,
        bool writeLatestFile = true)
    {
        lock (SyncRoot)
        {
            CloseWriters();

            string safeName = SanitizeFileName(sessionName);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _currentSessionPath = null;
            _currentSessionName = sessionName;
            _sessionStopwatch = System.Diagnostics.Stopwatch.StartNew();

            if (writeSessionFile || writeLatestFile)
                Directory.CreateDirectory(PathHelper.LogsDir);

            var encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            if (writeSessionFile)
            {
                _currentSessionPath = Path.Combine(PathHelper.LogsDir, $"{timestamp}_{safeName}.log");
                _sessionWriter = new StreamWriter(_currentSessionPath, append: false, encoding) { AutoFlush = true };
            }

            if (writeLatestFile)
            {
                string latestPath = Path.Combine(PathHelper.LogsDir, "latest.log");
                _latestWriter = new StreamWriter(latestPath, append: false, encoding) { AutoFlush = true };
            }

            string sinkDescription = _currentSessionPath ?? (writeLatestFile ? "latest.log only" : "console only");
            WriteDirect($"[Logger] Session started: '{sessionName}' -> {sinkDescription}");
        }
    }

    /// <summary>
    /// 结束当前日志会话，关闭文件句柄。
    /// </summary>
    public static void EndSession()
    {
        lock (SyncRoot)
        {
            if (_sessionWriter == null && _latestWriter == null)
                return;

            WriteDirect("[Logger] Session ended.");
            CloseWriters();
        }
    }

    public static void Log(string message)
    {
        var line = FormatLine(message);
        Console.WriteLine(line);
        lock (SyncRoot)
        {
            _sessionWriter?.WriteLine(line);
            _latestWriter?.WriteLine(line);
        }
        OnLog?.Invoke(line);
    }

    private static void WriteDirect(string message)
    {
        string line = FormatLine(message);
        Console.WriteLine(line);
        _sessionWriter?.WriteLine(line);
        _latestWriter?.WriteLine(line);
        OnLog?.Invoke(line);
    }

    private static void CloseWriters()
    {
        _sessionWriter?.Dispose();
        _latestWriter?.Dispose();
        _sessionWriter = null;
        _latestWriter = null;
        _currentSessionPath = null;
        _currentSessionName = null;
        _sessionStopwatch?.Stop();
        _sessionStopwatch = null;
    }

    private static string FormatLine(string message)
    {
        DateTime now = DateTime.Now;
        TimeSpan elapsed = _sessionStopwatch?.Elapsed ?? TimeSpan.Zero;
        string elapsedPart = _sessionStopwatch is null
            ? ""
            : $" +{elapsed:hh\\:mm\\:ss\\.fff}";
        return $"[{now:HH:mm:ss.fff}{elapsedPart}] {message}";
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "session";

        var chars = value.Select(ch =>
        {
            if (char.IsLetterOrDigit(ch))
                return ch;
            return ch is '-' or '_' ? ch : '_';
        }).ToArray();

        return new string(chars).Trim('_');
    }
}
