namespace LastRide.Models;

/// <summary>
/// Runtime Setup-Roles configuration for a single guild: the staff roles allowed
/// to run dynamic role commands, plus the command name → role mapping those
/// commands are built from. Held in the service cache and read by the message
/// pipeline. Mutations are performed copy-on-write so readers always see a
/// consistent snapshot without locking.
/// </summary>
public sealed class SetupRoleConfig
{
    public ulong GuildId { get; init; }

    public HashSet<ulong> StaffRoleIds { get; init; } = new();

    // Keyed case-insensitively on the command name so `?VIP` and `?vip` resolve
    // to the same entry, matching the case-insensitive prefix commands.
    public Dictionary<string, ulong> Commands { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<KeyValuePair<string, ulong>> OrderedCommands =>
        Commands.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    public bool HasStaffRole(IEnumerable<ulong> roleIds)
    {
        return StaffRoleIds.Count != 0 && roleIds.Any(StaffRoleIds.Contains);
    }

    public bool TryGetCommandRole(string name, out ulong roleId)
    {
        roleId = 0;

        return !string.IsNullOrWhiteSpace(name) &&
            Commands.TryGetValue(name, out roleId);
    }

    public SetupRoleConfig Clone()
    {
        return new SetupRoleConfig
        {
            GuildId = GuildId,
            StaffRoleIds = new HashSet<ulong>(StaffRoleIds),
            Commands = new Dictionary<string, ulong>(
                Commands,
                StringComparer.OrdinalIgnoreCase)
        };
    }
}
