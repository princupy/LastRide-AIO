namespace LastRide.Models;

/// <summary>
/// Runtime AutoMod configuration for a single guild. Held in the service cache
/// and read by the scanning engine. Mutations are performed copy-on-write so the
/// scanner always reads a consistent snapshot without locking.
/// </summary>
public sealed class AutoModConfig
{
    public ulong GuildId { get; init; }

    public bool Enabled { get; set; }

    public ulong? LogChannelId { get; set; }

    public HashSet<ulong> BypassRoleIds { get; init; } = new();

    public HashSet<string> BadWords { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<AutoModRuleType, AutoModRuleState> Rules { get; init; } = new();

    public bool IsRuleEnabled(AutoModRuleType rule)
    {
        return Rules.TryGetValue(rule, out var state) && state.Enabled;
    }

    public AutoModAction GetAction(AutoModRuleType rule)
    {
        return Rules.TryGetValue(rule, out var state)
            ? state.Action
            : AutoModAction.Delete;
    }

    public bool HasBypassRole(IEnumerable<ulong> roleIds)
    {
        if (BypassRoleIds.Count == 0)
            return false;

        foreach (var roleId in roleIds)
        {
            if (BypassRoleIds.Contains(roleId))
                return true;
        }

        return false;
    }

    public AutoModConfig Clone()
    {
        var clone = new AutoModConfig
        {
            GuildId = GuildId,
            Enabled = Enabled,
            LogChannelId = LogChannelId,
            BypassRoleIds = new HashSet<ulong>(BypassRoleIds),
            BadWords = new HashSet<string>(BadWords, StringComparer.OrdinalIgnoreCase)
        };

        foreach (var (rule, state) in Rules)
        {
            clone.Rules[rule] = new AutoModRuleState
            {
                Enabled = state.Enabled,
                Action = state.Action
            };
        }

        return clone;
    }
}

public sealed class AutoModRuleState
{
    public bool Enabled { get; set; }

    public AutoModAction Action { get; set; } = AutoModAction.Delete;
}
