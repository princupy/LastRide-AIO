namespace LastRide.Builders;

public enum SnipeAction
{
    Previous,
    Next
}

public static class SnipeComponentIds
{
    private const string Prefix = "snipe";

    public static string Create(
        SnipeAction action,
        ulong requesterId,
        ulong channelId,
        int index)
    {
        return $"{Prefix}:{ToId(action)}:{requesterId}:{channelId}:{index}";
    }

    public static bool TryParse(
        string? customId,
        out SnipeAction action,
        out ulong requesterId,
        out ulong channelId,
        out int index)
    {
        action = SnipeAction.Next;
        requesterId = 0;
        channelId = 0;
        index = 0;

        if (string.IsNullOrWhiteSpace(customId))
            return false;

        var parts = customId.Split(':');

        if (parts.Length != 5 ||
            parts[0] != Prefix ||
            !ulong.TryParse(parts[2], out requesterId) ||
            !ulong.TryParse(parts[3], out channelId) ||
            !int.TryParse(parts[4], out index))
        {
            return false;
        }

        action = parts[1] switch
        {
            "previous" => SnipeAction.Previous,
            "next" => SnipeAction.Next,
            _ => SnipeAction.Next
        };

        return parts[1] is "previous" or "next";
    }

    private static string ToId(SnipeAction action)
    {
        return action switch
        {
            SnipeAction.Previous => "previous",
            SnipeAction.Next => "next",
            _ => "next"
        };
    }
}
