namespace LastRide.Builders;

public enum BanListAction
{
    Previous,
    Next,
    Unban,
    UnbanAll
}

public static class BanListComponentIds
{
    private const string Prefix = "banlist";

    public static string CreateNav(
        BanListAction action,
        string sessionId,
        int page)
    {
        return $"{Prefix}:{ToId(action)}:{sessionId}:{page}:0";
    }

    public static string CreateUnbanAll(string sessionId, int page)
    {
        return $"{Prefix}:unbanall:{sessionId}:{page}:0";
    }

    public static string CreateUnban(string sessionId, int page, ulong targetId)
    {
        return $"{Prefix}:unban:{sessionId}:{page}:{targetId}";
    }

    public static bool TryParse(
        string? customId,
        out BanListAction action,
        out string sessionId,
        out int page,
        out ulong targetId)
    {
        action = BanListAction.Next;
        sessionId = string.Empty;
        page = 0;
        targetId = 0;

        if (string.IsNullOrWhiteSpace(customId))
            return false;

        var parts = customId.Split(':');

        if (parts.Length != 5 ||
            parts[0] != Prefix ||
            string.IsNullOrWhiteSpace(parts[2]) ||
            !int.TryParse(parts[3], out page) ||
            !ulong.TryParse(parts[4], out targetId))
        {
            return false;
        }

        sessionId = parts[2];

        action = parts[1] switch
        {
            "previous" => BanListAction.Previous,
            "next" => BanListAction.Next,
            "unban" => BanListAction.Unban,
            "unbanall" => BanListAction.UnbanAll,
            _ => BanListAction.Next
        };

        return parts[1] is "previous" or "next" or "unban" or "unbanall";
    }

    private static string ToId(BanListAction action)
    {
        return action switch
        {
            BanListAction.Previous => "previous",
            BanListAction.Next => "next",
            BanListAction.Unban => "unban",
            BanListAction.UnbanAll => "unbanall",
            _ => "next"
        };
    }
}
