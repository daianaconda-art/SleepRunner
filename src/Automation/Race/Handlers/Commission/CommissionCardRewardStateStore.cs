using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Handlers.Commission;

internal sealed class CommissionCardRewardStateStore
{
    private static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(15);
    private readonly object _lock = new();
    private bool _pendingRedDifficultCommissionReward;
    private DateTime _updatedAtUtc = DateTime.MinValue;

    public void MarkRedDifficultCommissionStarted()
    {
        lock (_lock)
        {
            _pendingRedDifficultCommissionReward = true;
            _updatedAtUtc = DateTime.UtcNow;
        }

        Logger.Log("[Race:Commission] Red difficult commission started; next card reward marked.");
    }

    public bool ConsumeRedDifficultCommissionReward()
    {
        lock (_lock)
        {
            if (!_pendingRedDifficultCommissionReward)
                return false;

            if (DateTime.UtcNow - _updatedAtUtc > PendingTtl)
            {
                _pendingRedDifficultCommissionReward = false;
                Logger.Log("[Race:Commission] Red difficult commission card reward marker expired.");
                return false;
            }

            _pendingRedDifficultCommissionReward = false;
            Logger.Log("[Race:Commission] Red difficult commission card reward marker consumed.");
            return true;
        }
    }
}
