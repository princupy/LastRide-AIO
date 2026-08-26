namespace LastRide.Builders;

public enum HelpCategory
{
    Home,
    AutoMod,
    AutoRole,
    Voice,
    Leveling,
    SetupRoles,
    Welcome,
    Ticket,
    Media,
    Logs,
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
            "leveling" => HelpCategory.Leveling,
            "setuproles" => HelpCategory.SetupRoles,
            "welcome" => HelpCategory.Welcome,
            "ticket" => HelpCategory.Ticket,
            "media" => HelpCategory.Media,
            "logs" => HelpCategory.Logs,
            "utility" => HelpCategory.Utility,
            "moderation" => HelpCategory.Moderation,
            _ => HelpCategory.Utility
        };

        return value is "home" or "automod" or "autorole" or "voice" or "leveling" or "setuproles" or "welcome" or "ticket" or "media" or "logs" or "utility" or "moderation";
    }

    public static string ToValue(HelpCategory category)
    {
        return category switch
        {
            HelpCategory.Home => "home",
            HelpCategory.AutoMod => "automod",
            HelpCategory.AutoRole => "autorole",
            HelpCategory.Voice => "voice",
            HelpCategory.Leveling => "leveling",
            HelpCategory.SetupRoles => "setuproles",
            HelpCategory.Welcome => "welcome",
            HelpCategory.Ticket => "ticket",
            HelpCategory.Media => "media",
            HelpCategory.Logs => "logs",
            HelpCategory.Utility => "utility",
            HelpCategory.Moderation => "moderation",
            _ => "utility"
        };
    }
}
