namespace LastRide.Builders;

public enum TicketAction
{
    New,
    Close,
    Claim,
    Unclaim,
    Reopen,
    Delete,
    Transcript
}

public static class TicketComponentIds
{
    private const string Prefix = "ticket";

    /// <summary>
    /// Builds a ticket button id. Unlike every other panel in the bot these ids
    /// carry no requester, because the create button on a ticket panel has to be
    /// usable by any member; permission is checked at click time instead. The
    /// target is the guild id for <see cref="TicketAction.New"/> and the ticket's
    /// channel id for every other action.
    /// </summary>
    public static string Create(TicketAction action, ulong targetId)
    {
        return $"{Prefix}:{ToSlug(action)}:{targetId}";
    }

    public static bool TryParse(
        string? customId,
        out TicketAction action,
        out ulong targetId)
    {
        action = TicketAction.New;
        targetId = 0;

        if (string.IsNullOrWhiteSpace(customId))
            return false;

        var parts = customId.Split(':');

        if (parts.Length != 3 || parts[0] != Prefix)
            return false;

        if (!TryParseAction(parts[1], out action))
            return false;

        return ulong.TryParse(parts[2], out targetId);
    }

    private static bool TryParseAction(string value, out TicketAction action)
    {
        action = TicketAction.New;

        switch (value)
        {
            case "new":
                action = TicketAction.New;
                return true;

            case "close":
                action = TicketAction.Close;
                return true;

            case "claim":
                action = TicketAction.Claim;
                return true;

            case "unclaim":
                action = TicketAction.Unclaim;
                return true;

            case "reopen":
                action = TicketAction.Reopen;
                return true;

            case "delete":
                action = TicketAction.Delete;
                return true;

            case "transcript":
                action = TicketAction.Transcript;
                return true;

            default:
                return false;
        }
    }

    private static string ToSlug(TicketAction action)
    {
        return action switch
        {
            TicketAction.New => "new",
            TicketAction.Close => "close",
            TicketAction.Claim => "claim",
            TicketAction.Unclaim => "unclaim",
            TicketAction.Reopen => "reopen",
            TicketAction.Delete => "delete",
            TicketAction.Transcript => "transcript",
            _ => "new"
        };
    }
}
