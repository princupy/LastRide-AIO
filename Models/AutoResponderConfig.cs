namespace LastRide.Models;

/// <summary>
/// Runtime autoresponder configuration for a single guild: a set of trigger
/// phrases mapped to the reply the bot posts when a message contains one. Held
/// in the service cache and read by the message pipeline. Mutations are
/// performed copy-on-write so readers always see a consistent snapshot without
/// locking.
/// </summary>
public sealed class AutoResponderConfig
{
    public ulong GuildId { get; init; }

    // Keyed case-insensitively on the trigger so lookups and duplicate checks
    // ignore casing while the original text is preserved for display.
    public Dictionary<string, string> Responses { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public AutoResponderConfig Clone()
    {
        return new AutoResponderConfig
        {
            GuildId = GuildId,
            Responses = new Dictionary<string, string>(
                Responses,
                StringComparer.OrdinalIgnoreCase)
        };
    }
}
