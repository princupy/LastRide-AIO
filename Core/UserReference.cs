using Discord;

namespace LastRide.Core;

/// <summary>
/// Turns a command argument into the ID of exactly one person, or refuses outright.
///
/// Every command that acts on a member used to fall back to a substring search over
/// usernames and nicknames, which meant a single letter was a valid target: <c>?mute a</c>
/// timed out whichever member happened to have an "a" somewhere in their name, and the
/// same token in front of <c>?ban</c> or <c>?kick</c> chose its victim just as casually.
/// A moderation action has to land on the person the moderator meant, so a token that
/// could stand for several members is now rejected instead of guessed at.
/// </summary>
internal static class UserReference
{
    /// <summary>
    /// Discord snowflakes are 17-20 digits and always will be — the timestamp field is
    /// wide enough that the length cannot grow for centuries. Holding bare digits to that
    /// shape lets a stray number ("?mute 5 spam") read as a forgotten mention rather than
    /// as a member who has left the server.
    /// </summary>
    private const int MinSnowflakeLength = 17;

    private const int MaxSnowflakeLength = 20;

    /// <summary>
    /// True when <paramref name="token"/> names one specific account: a mention, or a raw
    /// user ID. Nothing else qualifies, names included.
    /// </summary>
    public static bool TryParse(string? token, out ulong userId)
    {
        userId = 0;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        var trimmed = token.Trim();

        if (MentionUtils.TryParseUser(trimmed, out var mentionedId))
        {
            userId = mentionedId;
            return true;
        }

        if (trimmed.Length is < MinSnowflakeLength or > MaxSnowflakeLength)
            return false;

        // Digits only: ulong.TryParse would otherwise accept a leading sign, and "+123..."
        // is a typo rather than an ID.
        return trimmed.All(char.IsAsciiDigit) &&
               ulong.TryParse(trimmed, out userId);
    }
}
