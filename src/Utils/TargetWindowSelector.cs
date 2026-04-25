namespace SleepRunner.Utils;

public readonly record struct TargetWindowCandidate(
    nint Handle,
    int ProcessId,
    string ClassName,
    int ClientArea,
    bool IsVisible);

public static class TargetWindowSelector
{
    public static TargetWindowCandidate? SelectBestCandidate(
        IEnumerable<TargetWindowCandidate> candidates,
        int currentProcessId)
    {
        TargetWindowCandidate? best = null;
        foreach (TargetWindowCandidate candidate in candidates)
        {
            if (!candidate.IsVisible ||
                candidate.ProcessId == currentProcessId ||
                candidate.ClientArea <= 0 ||
                !candidate.ClassName.Contains("Unity", StringComparison.Ordinal))
            {
                continue;
            }

            if (best is null || candidate.ClientArea > best.Value.ClientArea)
                best = candidate;
        }

        return best;
    }
}
