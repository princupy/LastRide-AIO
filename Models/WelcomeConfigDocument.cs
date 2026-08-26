using MongoDB.Bson.Serialization.Attributes;

namespace LastRide.Models;

/// <summary>
/// MongoDB persistence shape for a guild's welcome configuration. The channel
/// snowflake is stored as a string to stay clear of BSON's signed 64-bit integer
/// range.
/// </summary>
public sealed class WelcomeConfigDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("enabled")]
    public bool Enabled { get; set; }

    [BsonElement("channelId")]
    public string? ChannelId { get; set; }

    [BsonElement("message")]
    public string? Message { get; set; }
}
