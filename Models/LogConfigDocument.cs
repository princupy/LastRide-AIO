using MongoDB.Bson.Serialization.Attributes;

namespace LastRide.Models;

/// <summary>
/// MongoDB persistence shape for a guild's logging configuration. Snowflake IDs
/// are stored as strings to stay clear of BSON's signed 64-bit integer range.
/// </summary>
public sealed class LogConfigDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("enabled")]
    public bool Enabled { get; set; }

    [BsonElement("channels")]
    public List<LogChannelEntry> Channels { get; set; } = new();
}

public sealed class LogChannelEntry
{
    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("channelId")]
    public string ChannelId { get; set; } = string.Empty;
}
