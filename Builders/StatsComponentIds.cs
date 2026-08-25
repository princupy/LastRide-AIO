namespace LastRide.Builders;

public enum StatsPanelTab
{
    General,
    Developer,
    Team
}

public static class StatsComponentIds
{
    private const string Prefix = "stats";

    public static string Create(StatsPanelTab tab, ulong userId)
    {
        return $"{Prefix}:{ToId(tab)}:{userId}";
    }

    public static bool TryParse(
        string customId,
        out StatsPanelTab tab,
        out ulong userId)
    {
        tab = StatsPanelTab.General;
        userId = 0;

        var parts = customId.Split(':');

        if (parts.Length != 3 ||
            parts[0] != Prefix ||
            !ulong.TryParse(parts[2], out userId))
        {
            return false;
        }

        tab = parts[1] switch
        {
            "general" => StatsPanelTab.General,
            "developer" => StatsPanelTab.Developer,
            "team" => StatsPanelTab.Team,
            _ => StatsPanelTab.General
        };

        return parts[1] is "general" or "developer" or "team";
    }

    private static string ToId(StatsPanelTab tab)
    {
        return tab switch
        {
            StatsPanelTab.General => "general",
            StatsPanelTab.Developer => "developer",
            StatsPanelTab.Team => "team",
            _ => "general"
        };
    }
}
