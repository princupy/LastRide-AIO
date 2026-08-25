namespace LastRide.Builders;

public enum ServerInfoPage
{
    Overview,
    Members,
    Channels,
    Roles,
    Assets
}

public static class ServerInfoComponentIds
{
    private const string Prefix = "serverinfo";

    public static string Create(
        ServerInfoPage page,
        ulong requesterId,
        ulong guildId)
    {
        return $"{Prefix}:{ToId(page)}:{requesterId}:{guildId}";
    }

    public static bool TryParse(
        string customId,
        out ServerInfoPage page,
        out ulong requesterId,
        out ulong guildId)
    {
        page = ServerInfoPage.Overview;
        requesterId = 0;
        guildId = 0;

        var parts = customId.Split(':');

        if (parts.Length != 4 ||
            parts[0] != Prefix ||
            !ulong.TryParse(parts[2], out requesterId) ||
            !ulong.TryParse(parts[3], out guildId))
        {
            return false;
        }

        page = parts[1] switch
        {
            "overview" => ServerInfoPage.Overview,
            "members" => ServerInfoPage.Members,
            "channels" => ServerInfoPage.Channels,
            "roles" => ServerInfoPage.Roles,
            "assets" => ServerInfoPage.Assets,
            _ => ServerInfoPage.Overview
        };

        return parts[1] is "overview" or "members" or "channels" or "roles" or "assets";
    }

    private static string ToId(ServerInfoPage page)
    {
        return page switch
        {
            ServerInfoPage.Overview => "overview",
            ServerInfoPage.Members => "members",
            ServerInfoPage.Channels => "channels",
            ServerInfoPage.Roles => "roles",
            ServerInfoPage.Assets => "assets",
            _ => "overview"
        };
    }
}
