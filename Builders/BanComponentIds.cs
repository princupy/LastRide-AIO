namespace LastRide.Builders;

public enum BanAction
{
    Confirm,
    Cancel
}

public static class BanComponentIds
{
    private const string Prefix = "ban";

    public static string Create(BanAction action, string requestId)
    {
        return $"{Prefix}:{ToId(action)}:{requestId}";
    }

    public static bool TryParse(
        string customId,
        out BanAction action,
        out string requestId)
    {
        action = BanAction.Cancel;
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
            "confirm" => BanAction.Confirm,
            "cancel" => BanAction.Cancel,
            _ => BanAction.Cancel
        };
        requestId = parts[2];

        return parts[1] is "confirm" or "cancel";
    }

    private static string ToId(BanAction action)
    {
        return action switch
        {
            BanAction.Confirm => "confirm",
            BanAction.Cancel => "cancel",
            _ => "cancel"
        };
    }
}
