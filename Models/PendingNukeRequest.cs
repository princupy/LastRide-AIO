namespace LastRide.Models;

public sealed record PendingNukeRequest(
    string Id,
    ulong GuildId,
    ulong RequesterId,
    ulong ChannelId,
    string ChannelName,
    DateTimeOffset CreatedAt);
