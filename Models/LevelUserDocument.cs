using MongoDB.Bson.Serialization.Attributes;

namespace LastRide.Models;

/// <summary>
/// MongoDB persistence shape for one member's XP in one guild. The id is the
/// <c>guildId:userId</c> pair so a member can level independently per server.
/// Snowflakes are stored as strings to stay clear of BSON's signed 64-bit range.
/// </summary>
public sealed class LevelUserDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("guildId")]
    public string GuildId { get; set; } = string.Empty;

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("xp")]
    public long Xp { get; set; }

    [BsonElement("voiceXp")]
    public long VoiceXp { get; set; }
}
