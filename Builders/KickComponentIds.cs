namespace LastRide.Builders;

public enum KickAction
{
    Confirm,
    Cancel
}

public static class KickComponentIds
{
    private const string Prefix = "kick";

    public static string Create(KickAction action, string requestId)
    {
        return $"{Prefix}:{ToId(action)}:{requestId}";
    }

    public static bool TryParse(
        string customId,
        out KickAction action,
        out string requestId)
    {
        action = KickAction.Cancel;
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
            "confirm" => KickAction.Confirm,
            "cancel" => KickAction.Cancel,
            _ => KickAction.Cancel
        };
        requestId = parts[2];

        return parts[1] is "confirm" or "cancel";
    }

    private static string ToId(KickAction action)
    {
        return action switch
        {
            KickAction.Confirm => "confirm",
            KickAction.Cancel => "cancel",
            _ => "cancel"
        };
    }
}
