namespace LastRide.Models;

/// <summary>
/// The XP curve shared by both the text and voice tracks. Reaching level+1 from
/// level costs <c>5·level² + 50·level + 100</c> XP, so early levels come quickly
/// and later ones stretch out. Stored XP is always the cumulative total.
/// </summary>
public static class LevelMath
{
    /// <summary>XP needed to go from <paramref name="level"/> to the next one.</summary>
    public static long XpToReachNext(int level)
    {
        if (level < 0)
            level = 0;

        long value = level;
        return (5 * value * value) + (50 * value) + 100;
    }

    /// <summary>Cumulative XP required to sit exactly at <paramref name="level"/>.</summary>
    public static long TotalXpForLevel(int level)
    {
        if (level <= 0)
            return 0;

        if (level > LevelDefaults.MaxLevel)
            level = LevelDefaults.MaxLevel;

        long total = 0;

        for (var current = 0; current < level; current++)
            total += XpToReachNext(current);

        return total;
    }

    /// <summary>The level a cumulative XP total resolves to.</summary>
    public static int LevelForXp(long xp)
    {
        if (xp <= 0)
            return 0;

        var level = 0;
        long consumed = 0;

        while (level < LevelDefaults.MaxLevel)
        {
            var next = XpToReachNext(level);

            if (consumed + next > xp)
                break;

            consumed += next;
            level++;
        }

        return level;
    }

    /// <summary>Progress into the current level, in XP.</summary>
    public static long XpIntoCurrentLevel(long xp)
    {
        if (xp <= 0)
            return 0;

        return xp - TotalXpForLevel(LevelForXp(xp));
    }

    /// <summary>Size of the current level's XP bar.</summary>
    public static long XpSpanForLevel(int level)
    {
        return XpToReachNext(level);
    }
}
