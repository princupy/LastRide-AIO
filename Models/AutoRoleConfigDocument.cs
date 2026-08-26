using MongoDB.Bson.Serialization.Attributes;

namespace LastRide.Models;

/// <summary>
/// MongoDB persistence shape for a guild's auto-role configuration. Snowflake IDs
/// are stored as strings to stay clear of BSON's signed 64-bit integer range.
/// </summary>
public sealed class AutoRoleConfigDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("autoRoleEnabled")]
    public bool AutoRoleEnabled { get; set; }

    [BsonElement("humanRoleIds")]
    public List<string> HumanRoleIds { get; set; } = new();

    [BsonElement("botRoleIds")]
    public List<string> BotRoleIds { get; set; } = new();

    [BsonElement("vcRoleEnabled")]
    public bool VcRoleEnabled { get; set; }

    [BsonElement("vcRoleId")]
    public string? VcRoleId { get; set; }
}
