namespace LastRide.Models;

/// <summary>
/// Runtime welcome configuration for a single guild: the channel new members are
/// greeted in and the message template rendered for them. Held in the service
/// cache and read by the join hook. Mutations are performed copy-on-write so
/// readers always see a consistent snapshot without locking.
/// </summary>
public sealed class WelcomeConfig
{
    public ulong GuildId { get; init; }

    public bool Enabled { get; set; }

    public ulong? ChannelId { get; set; }

    public string? Message { get; set; }

    /// <summary>A greeting is only posted once it is enabled and has a channel.</summary>
    public bool IsReady => Enabled && ChannelId is not null;

    public WelcomeConfig Clone()
    {
        return new WelcomeConfig
        {
            GuildId = GuildId,
            Enabled = Enabled,
            ChannelId = ChannelId,
            Message = Message
        };
    }
}
