using Discord;

namespace LastRide.Builders;

/// <summary>
/// The one place the card styling lives. Every builder reads its accent from here, so the
/// bot's colour is changed once rather than in every file that draws a container.
/// </summary>
public static class ComponentTheme
{
    /// <summary>The stripe down the left edge of every container card.</summary>
    public static readonly Color AccentColor = new(0xC2, 0x17, 0xE4);
}
