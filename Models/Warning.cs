namespace LastRide.Models;

public sealed record Warning(
    ulong GuildId,
    ulong UserId,
    ulong ModeratorId,
    string Reason,
    DateTimeOffset CreatedAt);
