namespace LastRide.Models;

/// <summary>
/// The categories of guild events that can each be routed to their own log
/// channel. A type only logs when the master switch is on and a channel is set
/// for it.
/// </summary>
public enum LogType
{
    Messages,
    Members,
    Voice,
    Moderation,
    Roles,
    Server
}

public static class LogTypeExtensions
{
    public static string DisplayName(this LogType type)
    {
        return type switch
        {
            LogType.Messages => "Messages",
            LogType.Members => "Members",
            LogType.Voice => "Voice",
            LogType.Moderation => "Moderation",
            LogType.Roles => "Roles",
            LogType.Server => "Server",
            _ => type.ToString()
        };
    }

    public static bool TryParse(string? token, out LogType type)
    {
        type = LogType.Messages;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        switch (token.Trim().ToLowerInvariant())
        {
            case "messages":
            case "message":
            case "msg":
            case "msgs":
                type = LogType.Messages;
                return true;
            case "members":
            case "member":
            case "mem":
            case "joinleave":
                type = LogType.Members;
                return true;
            case "voice":
            case "vc":
            case "voicelog":
                type = LogType.Voice;
                return true;
            case "moderation":
            case "mod":
            case "mods":
                type = LogType.Moderation;
                return true;
            case "roles":
            case "role":
            case "rolelog":
            case "rolelogs":
                type = LogType.Roles;
                return true;
            case "server":
            case "serverlog":
            case "serverlogs":
            case "guild":
                type = LogType.Server;
                return true;
            default:
                return false;
        }
    }
}
