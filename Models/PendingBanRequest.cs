namespace LastRide.Models;

public sealed record PendingBanRequest(
    string Id,
    ulong GuildId,
    ulong RequesterId,
    ulong TargetId,
    string TargetName,
    string? TargetAvatarUrl,
    string Reason,
    DateTimeOffset CreatedAt);
