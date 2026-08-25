namespace LastRide.Models;

public sealed record AfkStatus(
    ulong UserId,
    string DisplayName,
    string Reason,
    DateTimeOffset StartedAt);
