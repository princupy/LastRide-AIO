namespace LastRide.Builders;

/// <summary>Buttons on the player card.</summary>
public enum MusicControl
{
    Resume,
    Pause,
    Skip
}

/// <summary>
/// Custom-ID plumbing for the queue page buttons and the player card controls.
/// Everything the handler needs travels inside the id, so no server-side session is kept
/// and a page flip or a control press always re-reads the live player instead of a
/// snapshot taken when the card was posted.
/// </summary>
public static class MusicComponentIds
{
    private const string QueuePrefix = "music:queue";
    private const string ControlPrefix = "music:control";

    public static string CreateQueueNav(int page, ulong requesterId)
    {
        return $"{QueuePrefix}:{page}:{requesterId}";
    }

    public static bool TryParseQueueNav(
        string? customId,
        out int page,
        out ulong requesterId)
    {
        page = 0;
        requesterId = 0;

        if (string.IsNullOrWhiteSpace(customId))
            return false;

        var parts = customId.Split(':');

        return parts.Length == 4 &&
            parts[0] == "music" &&
            parts[1] == "queue" &&
            int.TryParse(parts[2], out page) &&
            ulong.TryParse(parts[3], out requesterId);
    }

    /// <summary>
    /// The control ids carry no requester: these buttons steer the whole channel's
    /// playback, not a panel belonging to one member, so the handler gates on being in
    /// the bot's voice channel instead.
    /// </summary>
    public static string CreateControl(MusicControl control)
    {
        return $"{ControlPrefix}:{Name(control)}";
    }

    public static bool TryParseControl(string? customId, out MusicControl control)
    {
        control = MusicControl.Resume;

        if (string.IsNullOrWhiteSpace(customId))
            return false;

        var parts = customId.Split(':');

        if (parts.Length != 3 ||
            parts[0] != "music" ||
            parts[1] != "control")
        {
            return false;
        }

        switch (parts[2])
        {
            case "resume":
                control = MusicControl.Resume;
                return true;

            case "pause":
                control = MusicControl.Pause;
                return true;

            case "skip":
                control = MusicControl.Skip;
                return true;

            default:
                return false;
        }
    }

    private static string Name(MusicControl control)
    {
        return control switch
        {
            MusicControl.Pause => "pause",
            MusicControl.Skip => "skip",
            _ => "resume"
        };
    }
}
