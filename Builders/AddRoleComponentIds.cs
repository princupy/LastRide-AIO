namespace LastRide.Builders;

public static class AddRoleComponentIds
{
    private const string RemovePrefix = "addrole:remove";

    public static string CreateRemove(
        ulong requesterId,
        ulong guildId,
        ulong targetId,
        ulong roleId)
    {
        return $"{RemovePrefix}:{requesterId}:{guildId}:{targetId}:{roleId}";
    }

    public static bool TryParseRemove(
        string? customId,
        out ulong requesterId,
        out ulong guildId,
        out ulong targetId,
        out ulong roleId)
    {
        requesterId = 0;
        guildId = 0;
        targetId = 0;
        roleId = 0;

        if (string.IsNullOrWhiteSpace(customId))
            return false;

        var parts = customId.Split(':');

        if (parts.Length != 6 ||
            parts[0] != "addrole" ||
            parts[1] != "remove")
        {
            return false;
        }

        return ulong.TryParse(parts[2], out requesterId) &&
            ulong.TryParse(parts[3], out guildId) &&
            ulong.TryParse(parts[4], out targetId) &&
            ulong.TryParse(parts[5], out roleId);
    }
}
