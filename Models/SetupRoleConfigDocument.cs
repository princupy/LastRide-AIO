using MongoDB.Bson.Serialization.Attributes;

namespace LastRide.Models;

/// <summary>
/// MongoDB persistence shape for a guild's Setup-Roles configuration. Snowflakes
/// are stored as strings so 64-bit ids survive the BSON round-trip, and the
/// command map is flattened into a list because a command name may contain
/// characters that are illegal in BSON field names.
/// </summary>
public sealed class SetupRoleConfigDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("staffRoles")]
    public List<string> StaffRoles { get; set; } = new();

    [BsonElement("commands")]
    public List<SetupRoleCommandEntry> Commands { get; set; } = new();
}

public sealed class SetupRoleCommandEntry
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("roleId")]
    public string RoleId { get; set; } = string.Empty;
}
