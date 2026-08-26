namespace LastRide.Builders;

/// <summary>Which giveaway button a custom id belongs to.</summary>
public enum GiveawayAction
{
    Enter,
    Entries
}

/// <summary>
/// Custom-ID plumbing for the giveaway buttons. Two shapes share one prefix
/// because the two surfaces have different audiences: the enter button carries no
/// requester id so any member can press it (permission is checked at click time),
/// while the entries navigation keeps its requester so only the member who ran the
/// command can flip pages.
/// </summary>
public static class GiveawayComponentIds
{
    private const string Prefix = "giveaway";

    /// <summary>
    /// Enter button. Deliberately requester-less, exactly like the ticket create
    /// button — it has to work for every member in the channel.
    /// </summary>
    public static string CreateEnter(ulong messageId)
    {
        return $"{Prefix}:enter:{messageId}";
    }

    /// <summary>
    /// Entries page navigation. Everything the handler needs travels inside the
    /// ID, so no server-side session is kept and a page flip always re-reads the
    /// live entry list.
    /// </summary>
    public static string CreateEntriesNav(ulong messageId, int page, ulong requesterId)
    {
        return $"{Prefix}:entries:{messageId}:{page}:{requesterId}";
    }

    public static bool TryParse(
        string? customId,
        out GiveawayAction action,
        out ulong messageId,
        out int page,
        out ulong requesterId)
    {
        action = GiveawayAction.Enter;
        messageId = 0;
        page = 0;
        requesterId = 0;

        if (string.IsNullOrWhiteSpace(customId))
            return false;

        var parts = customId.Split(':');

        if (parts.Length < 3 || parts[0] != Prefix)
            return false;

        if (!ulong.TryParse(parts[2], out messageId))
            return false;

        switch (parts[1])
        {
            case "enter":
                action = GiveawayAction.Enter;
                return parts.Length == 3;

            case "entries":
                action = GiveawayAction.Entries;

                return parts.Length == 5 &&
                    int.TryParse(parts[3], out page) &&
                    ulong.TryParse(parts[4], out requesterId);

            default:
                return false;
        }
    }
}
