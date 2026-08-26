namespace LastRide.Builders;

/// <summary>Which XP track a leaderboard page belongs to.</summary>
public enum LevelBoard
{
    Text,
    Voice
}

/// <summary>
/// Custom-ID plumbing for the leaderboard navigation buttons. Everything the
/// handler needs travels inside the ID, so no server-side session is kept and a
/// page flip always re-reads the live XP cache.
/// </summary>
public static class LevelComponentIds
{
    private const string Prefix = "lvllb";

    public static string Create(
        LevelBoard track,
        int page,
        ulong requesterId,
        ulong guildId)
    {
        return $"{Prefix}:{ToId(track)}:{page}:{requesterId}:{guildId}";
    }

    public static bool TryParse(
        string? customId,
        out LevelBoard track,
        out int page,
        out ulong requesterId,
        out ulong guildId)
    {
        track = LevelBoard.Text;
        page = 0;
        requesterId = 0;
        guildId = 0;

        if (string.IsNullOrWhiteSpace(customId))
            return false;

        var parts = customId.Split(':');

        if (parts.Length != 5 ||
            parts[0] != Prefix ||
            !int.TryParse(parts[2], out page) ||
            !ulong.TryParse(parts[3], out requesterId) ||
            !ulong.TryParse(parts[4], out guildId))
        {
            return false;
        }

        track = parts[1] switch
        {
            "voice" => LevelBoard.Voice,
            _ => LevelBoard.Text
        };

        return parts[1] is "text" or "voice";
    }

    private static string ToId(LevelBoard track)
    {
        return track switch
        {
            LevelBoard.Voice => "voice",
            _ => "text"
        };
    }
}
