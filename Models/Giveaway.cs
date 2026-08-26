namespace LastRide.Models;

/// <summary>
/// Runtime record for one giveaway. Keyed by the id of the message holding its
/// card, which is what the enter button, the entries pages, and every giveaway
/// command resolve against. Mutated copy-on-write like the guild configs.
/// </summary>
public sealed class Giveaway
{
    public ulong GuildId { get; init; }

    public ulong ChannelId { get; init; }

    /// <summary>Message carrying the giveaway card; also the cache key.</summary>
    public ulong MessageId { get; init; }

    /// <summary>Member who started the giveaway.</summary>
    public ulong HostId { get; init; }

    public string Prize { get; init; } = string.Empty;

    /// <summary>How many winners a draw should pick.</summary>
    public int WinnerCount { get; init; }

    /// <summary>Unix seconds; stored as a plain number so BSON keeps it exact.</summary>
    public long CreatedAt { get; init; }

    /// <summary>Unix seconds; stored as a plain number so BSON keeps it exact.</summary>
    public long EndsAt { get; set; }

    public bool IsEnded { get; set; }

    /// <summary>Members who pressed the enter button and have not left again.</summary>
    public HashSet<ulong> EntryIds { get; init; } = new();

    /// <summary>
    /// Winner forced by the owner-only rig command. Cleared by the first draw that
    /// uses it, so a reroll afterwards is genuinely random.
    /// </summary>
    public ulong? RiggedWinnerId { get; set; }

    /// <summary>Winners of the most recent draw; these are the ones on the card.</summary>
    public List<ulong> WinnerIds { get; set; } = new();

    /// <summary>
    /// Everyone drawn at any point, including previous rerolls. A draw excludes
    /// them so the same member never wins the same giveaway twice.
    /// </summary>
    public HashSet<ulong> PastWinnerIds { get; init; } = new();

    public bool IsRunning => !IsEnded;

    public DateTimeOffset EndsAtUtc => DateTimeOffset.FromUnixTimeSeconds(EndsAt);

    public bool HasExpired => EndsAtUtc <= DateTimeOffset.UtcNow;

    /// <summary>
    /// Built from the stored ids instead of fetching the message, which keeps the
    /// listing command free of one HTTP call per giveaway.
    /// </summary>
    public string JumpUrl =>
        $"https://discord.com/channels/{GuildId}/{ChannelId}/{MessageId}";

    public Giveaway Clone()
    {
        return new Giveaway
        {
            GuildId = GuildId,
            ChannelId = ChannelId,
            MessageId = MessageId,
            HostId = HostId,
            Prize = Prize,
            WinnerCount = WinnerCount,
            CreatedAt = CreatedAt,
            EndsAt = EndsAt,
            IsEnded = IsEnded,
            EntryIds = new HashSet<ulong>(EntryIds),
            RiggedWinnerId = RiggedWinnerId,
            WinnerIds = new List<ulong>(WinnerIds),
            PastWinnerIds = new HashSet<ulong>(PastWinnerIds)
        };
    }
}
