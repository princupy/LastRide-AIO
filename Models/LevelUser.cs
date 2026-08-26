namespace LastRide.Models;

/// <summary>
/// One member's XP in one guild. The text and voice tracks are counted
/// separately so each keeps its own level, rank, and leaderboard.
/// </summary>
public sealed class LevelUser
{
    public ulong GuildId { get; init; }

    public ulong UserId { get; init; }

    public long Xp { get; set; }

    public long VoiceXp { get; set; }

    public int TextLevel => LevelMath.LevelForXp(Xp);

    public int VoiceLevel => LevelMath.LevelForXp(VoiceXp);

    public LevelUser Clone()
    {
        return new LevelUser
        {
            GuildId = GuildId,
            UserId = UserId,
            Xp = Xp,
            VoiceXp = VoiceXp
        };
    }
}
