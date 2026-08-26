namespace LastRide.Models;

/// <summary>
/// Runtime logging configuration for a single guild. Held in the service cache.
/// Mutations are performed copy-on-write so readers always see a consistent
/// snapshot without locking. A log type fires only when <see cref="Enabled"/> is
/// true and a channel is set for that type.
/// </summary>
public sealed class LogConfig
{
    public ulong GuildId { get; init; }

    public bool Enabled { get; set; }

    public Dictionary<LogType, ulong> Channels { get; init; } = new();

    public ulong? GetChannel(LogType type)
    {
        return Channels.TryGetValue(type, out var channelId)
            ? channelId
            : null;
    }

    public LogConfig Clone()
    {
        var clone = new LogConfig
        {
            GuildId = GuildId,
            Enabled = Enabled
        };

        foreach (var (type, channelId) in Channels)
            clone.Channels[type] = channelId;

        return clone;
    }
}
