using MongoDB.Bson.Serialization.Attributes;

namespace LastRide.Models;

public sealed class GuildPrefixDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("prefix")]
    public string Prefix { get; set; } = string.Empty;
}
