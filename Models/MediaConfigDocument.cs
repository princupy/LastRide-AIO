using MongoDB.Bson.Serialization.Attributes;

namespace LastRide.Models;

/// <summary>
/// MongoDB persistence shape for a guild's media-only configuration. Channel
/// snowflakes are stored as strings to stay clear of BSON's signed 64-bit integer
/// range, and the channel set is flattened to a list.
/// </summary>
public sealed class MediaConfigDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("enabled")]
    public bool Enabled { get; set; }

    [BsonElement("channels")]
    public List<string> Channels { get; set; } = new();

    [BsonElement("chatChannel")]
    public string? ChatChannel { get; set; }
}
