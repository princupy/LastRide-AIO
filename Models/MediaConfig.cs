namespace LastRide.Models;

/// <summary>
/// Runtime media-only configuration for a single guild: the channels that accept
/// media only and the chat channel that removed messages are relayed to. Held in
/// the service cache and read on every guild message, so mutations are performed
/// copy-on-write and readers always see a consistent snapshot without locking.
/// </summary>
public sealed class MediaConfig
{
    public ulong GuildId { get; init; }

    public bool Enabled { get; set; }

    /// <summary>Channels enforced as media-only.</summary>
    public HashSet<ulong> ChannelIds { get; init; } = new();

    /// <summary>Where a removed message that mentioned someone is forwarded.</summary>
    public ulong? ChatChannelId { get; set; }

    /// <summary>Nothing is enforced until it is enabled and has a channel.</summary>
    public bool IsReady => Enabled && ChannelIds.Count > 0;

    /// <summary>Mention forwarding is optional — enforcement works without it.</summary>
    public bool ForwardsMentions => ChatChannelId is not null;

    public bool IsMediaChannel(ulong channelId)
    {
        return Enabled && ChannelIds.Contains(channelId);
    }

    public MediaConfig Clone()
    {
        return new MediaConfig
        {
            GuildId = GuildId,
            Enabled = Enabled,
            ChatChannelId = ChatChannelId,
            ChannelIds = new HashSet<ulong>(ChannelIds)
        };
    }
}
