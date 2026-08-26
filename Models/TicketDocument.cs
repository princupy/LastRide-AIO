using MongoDB.Bson.Serialization.Attributes;

namespace LastRide.Models;

/// <summary>
/// MongoDB persistence shape for a single ticket. The id is the channel
/// snowflake, which is already globally unique, so no composite key is needed.
/// Snowflakes are stored as strings to stay clear of BSON's signed 64-bit
/// integer range.
/// </summary>
public sealed class TicketDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("guildId")]
    public string GuildId { get; set; } = string.Empty;

    [BsonElement("ownerId")]
    public string OwnerId { get; set; } = string.Empty;

    [BsonElement("number")]
    public int Number { get; set; }

    [BsonElement("claimedBy")]
    public string? ClaimedBy { get; set; }

    [BsonElement("closed")]
    public bool Closed { get; set; }

    [BsonElement("createdAt")]
    public long CreatedAt { get; set; }

    [BsonElement("addedUsers")]
    public List<string> AddedUsers { get; set; } = new();
}
