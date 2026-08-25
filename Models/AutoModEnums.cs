namespace LastRide.Models;

public enum AutoModRuleType
{
    AntiCaps,
    AntiDuplicate,
    AntiEmoji,
    AntiInvite,
    AntiLink,
    AntiMention,
    AntiSpam,
    Badwords
}

public enum AutoModAction
{
    Delete,
    Warn,
    Mute,
    Kick,
    Ban
}

public static class AutoModRuleExtensions
{
    public static string DisplayName(this AutoModRuleType rule)
    {
        return rule switch
        {
            AutoModRuleType.AntiCaps => "Anti-Caps",
            AutoModRuleType.AntiDuplicate => "Anti-Duplicate",
            AutoModRuleType.AntiEmoji => "Anti-Emoji",
            AutoModRuleType.AntiInvite => "Anti-Invite",
            AutoModRuleType.AntiLink => "Anti-Link",
            AutoModRuleType.AntiMention => "Anti-Mention",
            AutoModRuleType.AntiSpam => "Anti-Spam",
            AutoModRuleType.Badwords => "Bad Words",
            _ => rule.ToString()
        };
    }

    public static string CommandName(this AutoModRuleType rule)
    {
        return rule.ToString().ToLowerInvariant();
    }
}

public static class AutoModActionExtensions
{
    public static string ToStorage(this AutoModAction action)
    {
        return action.ToString().ToLowerInvariant();
    }

    public static string ToDisplay(this AutoModAction action)
    {
        return action switch
        {
            AutoModAction.Delete => "Delete message",
            AutoModAction.Warn => "Delete + Warn",
            AutoModAction.Mute => "Delete + Mute",
            AutoModAction.Kick => "Delete + Kick",
            AutoModAction.Ban => "Delete + Ban",
            _ => "Delete message"
        };
    }

    public static bool TryParse(string? value, out AutoModAction action)
    {
        action = AutoModAction.Delete;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        switch (value.Trim().ToLowerInvariant())
        {
            case "delete":
                action = AutoModAction.Delete;
                return true;
            case "warn":
                action = AutoModAction.Warn;
                return true;
            case "mute":
            case "timeout":
                action = AutoModAction.Mute;
                return true;
            case "kick":
                action = AutoModAction.Kick;
                return true;
            case "ban":
                action = AutoModAction.Ban;
                return true;
            default:
                return false;
        }
    }
}
