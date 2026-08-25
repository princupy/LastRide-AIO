namespace LastRide.Builders;

public enum NukeAction
{
    Confirm,
    Cancel
}

public static class NukeComponentIds
{
    private const string Prefix = "nuke";

    public static string Create(NukeAction action, string requestId)
    {
        return $"{Prefix}:{ToId(action)}:{requestId}";
    }

    public static bool TryParse(
        string customId,
        out NukeAction action,
        out string requestId)
    {
        action = NukeAction.Cancel;
        requestId = string.Empty;

        var parts = customId.Split(':');

        if (parts.Length != 3 ||
            parts[0] != Prefix ||
            string.IsNullOrWhiteSpace(parts[2]))
        {
            return false;
        }

        action = parts[1] switch
        {
            "confirm" => NukeAction.Confirm,
            "cancel" => NukeAction.Cancel,
            _ => NukeAction.Cancel
        };
        requestId = parts[2];

        return parts[1] is "confirm" or "cancel";
    }

    private static string ToId(NukeAction action)
    {
        return action switch
        {
            NukeAction.Confirm => "confirm",
            NukeAction.Cancel => "cancel",
            _ => "cancel"
        };
    }
}
