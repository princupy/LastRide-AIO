namespace LastRide.Models;

public sealed record BanListSession(
    string Id,
    ulong GuildId,
    ulong RequesterId,
    List<BannedUser> Bans,
    DateTimeOffset CreatedAt);
