namespace LastRide.Models;

/// <summary>
/// How level-role rewards are handed out. <see cref="Stack"/> keeps every role a
/// member has earned; <see cref="Replace"/> keeps only the highest one and strips
/// the lower reward roles.
/// </summary>
public enum LevelRoleMode
{
    Stack,
    Replace
}

public static class LevelRoleModeExtensions
{
    public static string DisplayName(this LevelRoleMode mode)
    {
        return mode switch
        {
            LevelRoleMode.Stack => "Stack",
            LevelRoleMode.Replace => "Replace",
            _ => mode.ToString()
        };
    }

    public static string ToStorage(this LevelRoleMode mode)
    {
        return mode switch
        {
            LevelRoleMode.Replace => "replace",
            _ => "stack"
        };
    }

    public static bool TryParse(string? token, out LevelRoleMode mode)
    {
        mode = LevelRoleMode.Stack;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        switch (token.Trim().ToLowerInvariant())
        {
            case "stack":
            case "stacked":
            case "keep":
            case "all":
                mode = LevelRoleMode.Stack;
                return true;
            case "replace":
            case "single":
            case "highest":
            case "swap":
                mode = LevelRoleMode.Replace;
                return true;
            default:
                return false;
        }
    }
}
