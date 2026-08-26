namespace LastRide.Builders;

public static class LogComponentIds
{
    private const string MenuPrefix = "logs:setup";

    public static string CreateSetupMenu(
        ulong requesterId,
        ulong guildId)
    {
        return $"{MenuPrefix}:{requesterId}:{guildId}";
    }

    public static bool TryParseSetupMenu(
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
            parts[0] != "logs" ||
            parts[1] != "setup")
        {
            return false;
        }

        return ulong.TryParse(parts[2], out requesterId) &&
            ulong.TryParse(parts[3], out guildId);
    }
}
