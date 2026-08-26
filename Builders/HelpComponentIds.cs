namespace LastRide.Builders;

public enum HelpCategory
{
    Home,
    AutoMod,
    AutoRole,
    Voice,
    Utility,
    Moderation
}

public static class HelpComponentIds
{
    private const string Prefix = "help:category";

    public static string Create(ulong userId)
    {
        return $"{Prefix}:{userId}";
    }

    public static bool TryParse(string customId, out ulong userId)
    {
        userId = 0;

        if (!customId.StartsWith($"{Prefix}:"))
            return false;

        var value = customId[($"{Prefix}:").Length..];

        return ulong.TryParse(value, out userId);
    }

    public static bool TryParseCategory(
        string? value,
        out HelpCategory category)
    {
        category = HelpCategory.Utility;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        category = value switch
        {
            "home" => HelpCategory.Home,
            "automod" => HelpCategory.AutoMod,
            "autorole" => HelpCategory.AutoRole,
            "voice" => HelpCategory.Voice,
            "utility" => HelpCategory.Utility,
            "moderation" => HelpCategory.Moderation,
            _ => HelpCategory.Utility
        };

        return value is "home" or "automod" or "autorole" or "voice" or "utility" or "moderation";
    }

    public static string ToValue(HelpCategory category)
    {
        return category switch
        {
            HelpCategory.Home => "home",
            HelpCategory.AutoMod => "automod",
            HelpCategory.AutoRole => "autorole",
            HelpCategory.Voice => "voice",
            HelpCategory.Utility => "utility",
            HelpCategory.Moderation => "moderation",
            _ => "utility"
        };
    }
}
