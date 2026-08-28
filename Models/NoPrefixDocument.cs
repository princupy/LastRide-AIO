using MongoDB.Bson.Serialization.Attributes;

namespace LastRide.Models;

/// <summary>
/// MongoDB persistence shape for one no-prefix grant. The id is the member's
/// snowflake because the grant applies in every server, so no composite key is
/// needed. Snowflakes are stored as strings to stay clear of BSON's signed 64-bit
/// integer range.
/// </summary>
public sealed class NoPrefixDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("grantedBy")]
    public string GrantedBy { get; set; } = string.Empty;

    [BsonElement("grantedAt")]
    public long GrantedAt { get; set; }

    [BsonElement("expiresAt")]
    public long ExpiresAt { get; set; }

    [BsonElement("durationLabel")]
    public string DurationLabel { get; set; } = string.Empty;
}
