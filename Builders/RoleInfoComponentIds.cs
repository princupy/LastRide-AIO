namespace LastRide.Builders;

public enum RoleInfoPage
{
    Info,
    Members
}

public static class RoleInfoComponentIds
{
    private const string Prefix = "roleinfo";

    public static string Create(
        RoleInfoPage page,
        ulong requesterId,
        ulong guildId,
        ulong roleId)
    {
        return $"{Prefix}:{ToId(page)}:{requesterId}:{guildId}:{roleId}";
    }

    public static bool TryParse(
        string customId,
        out RoleInfoPage page,
        out ulong requesterId,
        out ulong guildId,
        out ulong roleId)
    {
        page = RoleInfoPage.Info;
        requesterId = 0;
        guildId = 0;
        roleId = 0;

        var parts = customId.Split(':');

        if (parts.Length != 5 ||
            parts[0] != Prefix ||
            !ulong.TryParse(parts[2], out requesterId) ||
            !ulong.TryParse(parts[3], out guildId) ||
            !ulong.TryParse(parts[4], out roleId))
        {
            return false;
        }

        page = parts[1] switch
        {
            "info" => RoleInfoPage.Info,
            "members" => RoleInfoPage.Members,
            _ => RoleInfoPage.Info
        };

        return parts[1] is "info" or "members";
    }

    private static string ToId(RoleInfoPage page)
    {
        return page switch
        {
            RoleInfoPage.Info => "info",
            RoleInfoPage.Members => "members",
            _ => "info"
        };
    }
}
