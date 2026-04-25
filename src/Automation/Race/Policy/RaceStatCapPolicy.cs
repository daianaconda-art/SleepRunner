namespace SleepRunner.Automation.Race.Policy;

public static class RaceStatCapPolicy
{
    public const int AttributeCap = 1250;

    public static bool IsStrengthCapped(int? stat)
    {
        return stat.HasValue && stat.Value >= AttributeCap;
    }

    public static bool IsStrengthCapped(int stat)
    {
        return stat >= AttributeCap;
    }

    public static bool IsStaminaCapped(int? stat)
    {
        return stat.HasValue && stat.Value >= AttributeCap;
    }

    public static bool IsStaminaCapped(int stat)
    {
        return stat >= AttributeCap;
    }
}
