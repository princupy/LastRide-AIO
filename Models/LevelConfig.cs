namespace LastRide.Models;

/// <summary>
/// Runtime leveling configuration for a single guild. Held in the service cache
/// and read by the XP engine. Mutations are performed copy-on-write so the engine
/// always reads a consistent snapshot without locking.
/// </summary>
public sealed class LevelConfig
{
    public ulong GuildId { get; init; }

    public bool Enabled { get; set; }

    public int XpCooldownSeconds { get; set; } = LevelDefaults.DefaultCooldownSeconds;

    public int MinXpPerMessage { get; set; } = LevelDefaults.DefaultMinXp;

    public int MaxXpPerMessage { get; set; } = LevelDefaults.DefaultMaxXp;

    public bool LevelUpAnnouncementsEnabled { get; set; } = true;

    public ulong? LevelUpChannelId { get; set; }

    public string? LevelUpMessage { get; set; }

    public LevelRoleMode RoleMode { get; set; } = LevelRoleMode.Stack;

    public HashSet<ulong> BlacklistedChannelIds { get; init; } = new();

    public HashSet<ulong> BlacklistedRoleIds { get; init; } = new();

    /// <summary>Level → reward role. Only the text track grants these.</summary>
    public Dictionary<int, ulong> LevelRoles { get; init; } = new();

    public bool IsChannelBlacklisted(ulong channelId)
    {
        return BlacklistedChannelIds.Count > 0 &&
            BlacklistedChannelIds.Contains(channelId);
    }

    public bool HasBlacklistedRole(IEnumerable<ulong> roleIds)
    {
        if (BlacklistedRoleIds.Count == 0)
            return false;

        foreach (var roleId in roleIds)
        {
            if (BlacklistedRoleIds.Contains(roleId))
                return true;
        }

        return false;
    }

    /// <summary>Every reward role a member at <paramref name="level"/> has earned.</summary>
    public IEnumerable<(int Level, ulong RoleId)> RolesUpTo(int level)
    {
        return LevelRoles
            .Where(pair => pair.Key <= level)
            .OrderBy(pair => pair.Key)
            .Select(pair => (pair.Key, pair.Value));
    }

    /// <summary>The single highest reward role earned at <paramref name="level"/>.</summary>
    public ulong? HighestRoleUpTo(int level)
    {
        ulong? roleId = null;
        var best = int.MinValue;

        foreach (var (rewardLevel, rewardRoleId) in LevelRoles)
        {
            if (rewardLevel <= level && rewardLevel > best)
            {
                best = rewardLevel;
                roleId = rewardRoleId;
            }
        }

        return roleId;
    }

    public LevelConfig Clone()
    {
        var clone = new LevelConfig
        {
            GuildId = GuildId,
            Enabled = Enabled,
            XpCooldownSeconds = XpCooldownSeconds,
            MinXpPerMessage = MinXpPerMessage,
            MaxXpPerMessage = MaxXpPerMessage,
            LevelUpAnnouncementsEnabled = LevelUpAnnouncementsEnabled,
            LevelUpChannelId = LevelUpChannelId,
            LevelUpMessage = LevelUpMessage,
            RoleMode = RoleMode,
            BlacklistedChannelIds = new HashSet<ulong>(BlacklistedChannelIds),
            BlacklistedRoleIds = new HashSet<ulong>(BlacklistedRoleIds)
        };

        foreach (var (level, roleId) in LevelRoles)
            clone.LevelRoles[level] = roleId;

        return clone;
    }
}
