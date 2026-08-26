namespace LastRide.Models;

/// <summary>
/// Tunable limits and defaults for the leveling system. Kept in one place so the
/// config service, module validation, and help text never drift apart.
/// </summary>
public static class LevelDefaults
{
    public const int DefaultCooldownSeconds = 60;
    public const int MinCooldownSeconds = 0;
    public const int MaxCooldownSeconds = 3600;

    /// <summary>Voice XP handed out for every full minute spent in a channel.</summary>
    public const int VoiceXpPerMinute = 10;

    public const int DefaultMinXp = 15;
    public const int DefaultMaxXp = 25;
    public const int MinXpPerMessage = 1;
    public const int MaxXpPerMessage = 500;

    public const int MaxLevelRoles = 25;
    public const int MaxBlacklistedChannels = 50;
    public const int MaxBlacklistedRoles = 25;

    public const int MaxLevel = 1000;
    public const int MaxXpGrant = 1_000_000;

    public const int MaxLevelUpMessageLength = 500;
}
