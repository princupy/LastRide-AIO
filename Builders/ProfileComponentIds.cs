namespace LastRide.Builders;

public enum AvatarView
{
    Global,
    Server
}

public static class ProfileComponentIds
{
    private const string Prefix = "profile:avatar";

    public static string Create(
        AvatarView view,
        ulong requesterId,
        ulong targetUserId,
        ulong guildId)
    {
        return $"{Prefix}:{ToId(view)}:{requesterId}:{targetUserId}:{guildId}";
    }

    public static bool TryParse(
        string customId,
        out AvatarView view,
        out ulong requesterId,
        out ulong targetUserId,
        out ulong guildId)
    {
        view = AvatarView.Global;
        requesterId = 0;
        targetUserId = 0;
        guildId = 0;

        var parts = customId.Split(':');

        if (parts.Length != 6 ||
            parts[0] != "profile" ||
            parts[1] != "avatar" ||
            !ulong.TryParse(parts[3], out requesterId) ||
            !ulong.TryParse(parts[4], out targetUserId) ||
            !ulong.TryParse(parts[5], out guildId))
        {
            return false;
        }

        view = parts[2] switch
        {
            "global" => AvatarView.Global,
            "server" => AvatarView.Server,
            _ => AvatarView.Global
        };

        return parts[2] is "global" or "server";
    }

    private static string ToId(AvatarView view)
    {
        return view switch
        {
            AvatarView.Global => "global",
            AvatarView.Server => "server",
            _ => "global"
        };
    }
}
