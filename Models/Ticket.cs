namespace LastRide.Models;

/// <summary>
/// Runtime record for one open or closed ticket channel. Keyed by channel id in
/// the service cache, which is what every button and in-ticket command resolves
/// against. Mutated copy-on-write like the guild configs.
/// </summary>
public sealed class Ticket
{
    public ulong GuildId { get; init; }

    public ulong ChannelId { get; init; }

    /// <summary>Member who opened the ticket.</summary>
    public ulong OwnerId { get; init; }

    /// <summary>Sequential number used for the channel name.</summary>
    public int Number { get; init; }

    /// <summary>Staff member who claimed the ticket, if any.</summary>
    public ulong? ClaimedBy { get; set; }

    public bool IsClosed { get; set; }

    /// <summary>Unix seconds; stored as a plain number so BSON keeps it exact.</summary>
    public long CreatedAt { get; init; }

    /// <summary>Extra members pulled in with the add command.</summary>
    public HashSet<ulong> AddedUserIds { get; init; } = new();

    public Ticket Clone()
    {
        return new Ticket
        {
            GuildId = GuildId,
            ChannelId = ChannelId,
            OwnerId = OwnerId,
            Number = Number,
            ClaimedBy = ClaimedBy,
            IsClosed = IsClosed,
            CreatedAt = CreatedAt,
            AddedUserIds = new HashSet<ulong>(AddedUserIds)
        };
    }
}
