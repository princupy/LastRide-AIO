namespace LastRide.Builders;

public static class VoiceComponentIds
{
    private const string MenuPrefix = "voice:menu";

    // The op discriminates what the dropdown does when a channel is picked:
    //   move    -> move a single member ({targetId}) into the chosen channel
    //   moveall -> move everyone in the requester's current VC into the chosen channel
    //   pullall -> pull everyone from the chosen channel into the requester's VC
    public static string CreateMenu(
        string op,
        ulong requesterId,
        ulong guildId,
        ulong targetId)
    {
        return $"{MenuPrefix}:{op}:{requesterId}:{guildId}:{targetId}";
    }

    public static string CreateChannelValue(ulong channelId)
    {
        return $"channel:{channelId}";
    }

    public static bool TryParseMenu(
        string? customId,
        out string op,
        out ulong requesterId,
        out ulong guildId,
        out ulong targetId)
    {
        op = string.Empty;
        requesterId = 0;
        guildId = 0;
        targetId = 0;

        if (string.IsNullOrWhiteSpace(customId))
            return false;

        var parts = customId.Split(':');

        if (parts.Length != 6 ||
            parts[0] != "voice" ||
            parts[1] != "menu")
        {
            return false;
        }

        op = parts[2];

        return op is "move" or "moveall" or "pullall" &&
            ulong.TryParse(parts[3], out requesterId) &&
            ulong.TryParse(parts[4], out guildId) &&
            ulong.TryParse(parts[5], out targetId);
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
