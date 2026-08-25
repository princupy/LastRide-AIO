namespace LastRide.Models;

public sealed record BannedUser(
    ulong UserId,
    string UserName,
    string AvatarUrl,
    string Reason);
