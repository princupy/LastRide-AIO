namespace LastRide.Builders;

public enum UnbanAction
{
    Confirm,
    Cancel
}

public static class UnbanComponentIds
{
    private const string Prefix = "unban";

    public static string Create(UnbanAction action, string requestId)
    {
        return $"{Prefix}:{ToId(action)}:{requestId}";
    }

    public static bool TryParse(
        string customId,
        out UnbanAction action,
        out string requestId)
    {
        action = UnbanAction.Cancel;
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
            "confirm" => UnbanAction.Confirm,
            "cancel" => UnbanAction.Cancel,
            _ => UnbanAction.Cancel
        };
        requestId = parts[2];

        return parts[1] is "confirm" or "cancel";
    }

    private static string ToId(UnbanAction action)
    {
        return action switch
        {
            UnbanAction.Confirm => "confirm",
            UnbanAction.Cancel => "cancel",
            _ => "cancel"
        };
    }
}
