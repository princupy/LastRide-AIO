using MongoDB.Bson.Serialization.Attributes;

namespace LastRide.Models;

/// <summary>
/// MongoDB persistence shape for a guild's AutoMod configuration. Snowflake IDs
/// are stored as strings to stay clear of BSON's signed 64-bit integer range.
/// </summary>
public sealed class AutoModConfigDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("enabled")]
    public bool Enabled { get; set; }

    [BsonElement("logChannelId")]
    public string? LogChannelId { get; set; }

    [BsonElement("bypassRoleIds")]
    public List<string> BypassRoleIds { get; set; } = new();

    [BsonElement("badWords")]
    public List<string> BadWords { get; set; } = new();

    [BsonElement("rules")]
    public List<AutoModRuleEntry> Rules { get; set; } = new();
}

public sealed class AutoModRuleEntry
{
    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("enabled")]
    public bool Enabled { get; set; }

    [BsonElement("action")]
    public string Action { get; set; } = "delete";
}
