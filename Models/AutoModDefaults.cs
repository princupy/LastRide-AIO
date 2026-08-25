namespace LastRide.Models;

public static class AutoModDefaults
{
    // Anti-Caps: flag messages with at least CapsMinLength letters where the
    // uppercase ratio meets or exceeds CapsRatio.
    public const int CapsMinLength = 10;
    public const double CapsRatio = 0.70;

    // Anti-Spam: flag when a user sends SpamMessages within SpamWindowSeconds.
    public const int SpamMessages = 5;
    public const int SpamWindowSeconds = 5;

    // Anti-Mention: flag messages with more than MaxMentions total mentions.
    public const int MaxMentions = 5;

    // Anti-Emoji: flag messages with more than MaxEmojis total emojis.
    public const int MaxEmojis = 5;

    // Anti-Duplicate: flag when the same message is repeated DuplicateCount
    // times in a row within DuplicateWindowSeconds.
    public const int DuplicateCount = 3;
    public const int DuplicateWindowSeconds = 30;

    // Mute action timeout duration.
    public const int MuteMinutes = 10;

    // Maximum number of bypass roles a guild may configure.
    public const int MaxBypassRoles = 15;

    // Bad-words filter: max entries per guild and max length of a single entry.
    public const int MaxBadWords = 100;
    public const int MaxBadWordLength = 50;
}
