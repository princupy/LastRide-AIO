using MongoDB.Bson.Serialization.Attributes;

namespace LastRide.Models;

/// <summary>
/// MongoDB persistence shape for a single giveaway. The id is the card's message
/// snowflake, which is already globally unique, so no composite key is needed.
/// Snowflakes are stored as strings to stay clear of BSON's signed 64-bit
/// integer range.
/// </summary>
public sealed class GiveawayDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("guildId")]
    public string GuildId { get; set; } = string.Empty;

    [BsonElement("channelId")]
    public string ChannelId { get; set; } = string.Empty;

    [BsonElement("hostId")]
    public string HostId { get; set; } = string.Empty;

    [BsonElement("prize")]
    public string Prize { get; set; } = string.Empty;

    [BsonElement("winnerCount")]
    public int WinnerCount { get; set; }

    [BsonElement("createdAt")]
    public long CreatedAt { get; set; }

    [BsonElement("endsAt")]
    public long EndsAt { get; set; }

    [BsonElement("ended")]
    public bool Ended { get; set; }

    [BsonElement("entries")]
    public List<string> Entries { get; set; } = new();

    [BsonElement("rigged")]
    public string? Rigged { get; set; }

    [BsonElement("winners")]
    public List<string> Winners { get; set; } = new();

    [BsonElement("pastWinners")]
    public List<string> PastWinners { get; set; } = new();
}
