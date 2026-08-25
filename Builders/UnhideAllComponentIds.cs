namespace LastRide.Builders;

public static class UnhideAllComponentIds
{
    private const string MenuPrefix = "unhideall:menu";
    public const string AllChannelsValue = "all";

    public static string CreateMenu(
        ulong requesterId,
        ulong guildId)
    {
        return $"{MenuPrefix}:{requesterId}:{guildId}";
    }

    public static string CreateChannelValue(ulong channelId)
    {
        return $"channel:{channelId}";
    }

    public static bool TryParseMenu(
        string? customId,
        out ulong requesterId,
        out ulong guildId)
    {
        requesterId = 0;
        guildId = 0;

        if (string.IsNullOrWhiteSpace(customId))
            return false;

        var parts = customId.Split(':');

        if (parts.Length != 4 ||
            parts[0] != "unhideall" ||
            parts[1] != "menu")
        {
            return false;
        }

        return ulong.TryParse(parts[2], out requesterId) &&
            ulong.TryParse(parts[3], out guildId);
    }

    public static bool TryParseChannelValue(
        string value,
        out ulong channelId)
    {
        channelId = 0;

        if (!value.StartsWith("channel:", StringComparison.Ordinal))
            return false;

        return ulong.TryParse(value["channel:".Length..], out channelId);
    }
}
