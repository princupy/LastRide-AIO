namespace LastRide.Builders;

public static class AutoModComponentIds
{
    private const string MenuPrefix = "automod:rules";

    public static string CreateRulesMenu(
        ulong requesterId,
        ulong guildId)
    {
        return $"{MenuPrefix}:{requesterId}:{guildId}";
    }

    public static bool TryParseRulesMenu(
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
            parts[0] != "automod" ||
            parts[1] != "rules")
        {
            return false;
        }

        return ulong.TryParse(parts[2], out requesterId) &&
            ulong.TryParse(parts[3], out guildId);
    }
}
