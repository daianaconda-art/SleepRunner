using System.Text.Json;
using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Handlers.Trade;

/// <summary>
/// 跨进程持久化"本轮 trade 是否已访问"
/// 支持 step-once 多次启动串联流程
/// </summary>
internal sealed class TradeStateStore
{
    private static readonly TimeSpan StageStateTtl = TimeSpan.FromMinutes(15);
    private readonly string _stateFilePath;

    public TradeStateStore()
    {
        _stateFilePath = Path.Combine(PathHelper.BaseDir, "assets", "race_trade_stage_state.json");
    }

    public bool LoadVisited()
    {
        try
        {
            if (!File.Exists(_stateFilePath))
                return false;

            var json = File.ReadAllText(_stateFilePath);
            var state = JsonSerializer.Deserialize<TradeStageState>(json);
            if (state == null)
                return false;

            if (DateTime.UtcNow - state.UpdatedAtUtc > StageStateTtl)
            {
                Logger.Log("[Race:Trade] Persisted trade state expired, reset.");
                TryDeleteFile();
                return false;
            }

            if (state.TradeVisited)
                Logger.Log("[Race:Trade] Persisted trade state loaded: tradeVisited=true");
            return state.TradeVisited;
        }
        catch (Exception ex)
        {
            Logger.Log($"[Race:Trade] Load persisted trade state failed: {ex.Message}");
            return false;
        }
    }

    public void SaveVisited(bool visited)
    {
        try
        {
            if (!visited)
            {
                Logger.Log("[Race:Trade] Persisted trade state cleared (tradeVisited=false).");
                TryDeleteFile();
                return;
            }

            string dir = Path.GetDirectoryName(_stateFilePath) ?? PathHelper.BaseDir;
            Directory.CreateDirectory(dir);
            var state = new TradeStageState
            {
                TradeVisited = true,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var json = JsonSerializer.Serialize(state);
            File.WriteAllText(_stateFilePath, json);
            Logger.Log($"[Race:Trade] Persisted trade state saved: tradeVisited=true, path='{_stateFilePath}'");
        }
        catch (Exception ex)
        {
            Logger.Log($"[Race:Trade] Save persisted trade state failed: {ex.Message}");
        }
    }

    private void TryDeleteFile()
    {
        try
        {
            if (File.Exists(_stateFilePath))
                File.Delete(_stateFilePath);
        }
        catch (Exception ex)
        {
            Logger.Log($"[Race:Trade] Delete persisted trade state failed: {ex.Message}");
        }
    }

    private sealed class TradeStageState
    {
        public bool TradeVisited { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
