using MongoDB.Bson.Serialization.Attributes;

namespace LastRide.Models;

/// <summary>
/// MongoDB persistence shape for a guild's ticket configuration. Snowflakes are
/// stored as strings to stay clear of BSON's signed 64-bit integer range.
/// </summary>
public sealed class TicketConfigDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("enabled")]
    public bool Enabled { get; set; }

    [BsonElement("categoryId")]
    public string? CategoryId { get; set; }

    [BsonElement("logChannelId")]
    public string? LogChannelId { get; set; }

    [BsonElement("supportRoles")]
    public List<string> SupportRoles { get; set; } = new();

    [BsonElement("openMessage")]
    public string? OpenMessage { get; set; }

    [BsonElement("panelMessage")]
    public string? PanelMessage { get; set; }

    [BsonElement("limit")]
    public int Limit { get; set; }

    [BsonElement("counter")]
    public int Counter { get; set; }
}
