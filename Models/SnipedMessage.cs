namespace LastRide.Models;

public sealed record SnipedMessage(
    ulong MessageId,
    ulong ChannelId,
    ulong AuthorId,
    string AuthorName,
    string? AuthorAvatarUrl,
    string Content,
    int AttachmentCount,
    ulong DeletedById,
    DateTimeOffset DeletedAt);
