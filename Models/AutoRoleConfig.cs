namespace LastRide.Models;

/// <summary>
/// Runtime auto-role configuration for a single guild: roles handed out on join
/// (split into human and bot lists) and a single role granted while a member is
/// in a voice channel. Held in the service cache and read by the gateway hooks.
/// Mutations are performed copy-on-write so readers always see a consistent
/// snapshot without locking.
/// </summary>
public sealed class AutoRoleConfig
{
    public ulong GuildId { get; init; }

    public bool AutoRoleEnabled { get; set; }

    public HashSet<ulong> HumanRoleIds { get; init; } = new();

    public HashSet<ulong> BotRoleIds { get; init; } = new();

    public bool VcRoleEnabled { get; set; }

    public ulong? VcRoleId { get; set; }

    public AutoRoleConfig Clone()
    {
        return new AutoRoleConfig
        {
            GuildId = GuildId,
            AutoRoleEnabled = AutoRoleEnabled,
            HumanRoleIds = new HashSet<ulong>(HumanRoleIds),
            BotRoleIds = new HashSet<ulong>(BotRoleIds),
            VcRoleEnabled = VcRoleEnabled,
            VcRoleId = VcRoleId
        };
    }
}
