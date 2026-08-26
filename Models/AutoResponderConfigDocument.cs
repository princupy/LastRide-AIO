using MongoDB.Bson.Serialization.Attributes;

namespace LastRide.Models;

/// <summary>
/// MongoDB persistence shape for a guild's autoresponder configuration. Triggers
/// are stored as a list of entries rather than a map because a trigger phrase
/// may contain characters (dots, <c>$</c>) that are illegal in BSON field names.
/// </summary>
public sealed class AutoResponderConfigDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("responses")]
    public List<AutoResponderEntryDocument> Responses { get; set; } = new();
}

public sealed class AutoResponderEntryDocument
{
    [BsonElement("trigger")]
    public string Trigger { get; set; } = string.Empty;

    [BsonElement("reply")]
    public string Reply { get; set; } = string.Empty;
}
