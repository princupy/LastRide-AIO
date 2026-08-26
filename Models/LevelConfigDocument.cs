using MongoDB.Bson.Serialization.Attributes;

namespace LastRide.Models;

/// <summary>
/// MongoDB persistence shape for a guild's leveling configuration. Snowflake IDs
/// are stored as strings to stay clear of BSON's signed 64-bit integer range.
/// </summary>
public sealed class LevelConfigDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("enabled")]
    public bool Enabled { get; set; }

    [BsonElement("xpCooldownSeconds")]
    public int XpCooldownSeconds { get; set; } = LevelDefaults.DefaultCooldownSeconds;

    [BsonElement("minXpPerMessage")]
    public int MinXpPerMessage { get; set; } = LevelDefaults.DefaultMinXp;

    [BsonElement("maxXpPerMessage")]
    public int MaxXpPerMessage { get; set; } = LevelDefaults.DefaultMaxXp;

    [BsonElement("levelUpAnnouncementsEnabled")]
    public bool LevelUpAnnouncementsEnabled { get; set; } = true;

    [BsonElement("levelUpChannelId")]
    public string? LevelUpChannelId { get; set; }

    [BsonElement("levelUpMessage")]
    public string? LevelUpMessage { get; set; }

    [BsonElement("roleMode")]
    public string RoleMode { get; set; } = "stack";

    [BsonElement("blacklistedChannelIds")]
    public List<string> BlacklistedChannelIds { get; set; } = new();

    [BsonElement("blacklistedRoleIds")]
    public List<string> BlacklistedRoleIds { get; set; } = new();

    [BsonElement("levelRoles")]
    public List<LevelRoleEntry> LevelRoles { get; set; } = new();
}

public sealed class LevelRoleEntry
{
    [BsonElement("level")]
    public int Level { get; set; }

    [BsonElement("roleId")]
    public string RoleId { get; set; } = string.Empty;
}
