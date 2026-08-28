using Discord;
using Discord.Commands;
using LastRide.Configuration;

namespace LastRide.Services;

/// <summary>
/// Answers "how many commands can this member actually run?" for the help menu and
/// the mention card.
///
/// The repo gates permissions inside the command bodies rather than through
/// <c>[RequireUserPermission]</c> preconditions, so the framework has nothing to read
/// back. This table mirrors those gates instead — one entry per command, using the
/// same <c>Func&lt;GuildPermissions, bool&gt;</c> shape the voice commands already pass
/// around, so each rule reads like the gate it stands for. It is counting metadata
/// only: nothing here grants or denies anything.
/// </summary>
public sealed class CommandAccessService
{
    private readonly CommandService _commands;
    private readonly BotOptions _options;

    /// <summary>
    /// Command name to the permission it needs. A <c>null</c> gate means every member
    /// may run it; a missing key is treated the same way but reported by
    /// <see cref="Validate"/>.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Func<GuildPermissions, bool>?> Gates =
        BuildGates();

    /// <summary>
    /// Commands only the configured owner may run. Kept apart from the hidden marker
    /// on purpose — "not listed in the help menu" and "only the owner may run it" are
    /// two different things that happen to coincide today.
    /// </summary>
    private static readonly HashSet<string> OwnerOnly =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "setwinner",
            "nop"
        };

    public CommandAccessService(CommandService commands, BotOptions options)
    {
        _commands = commands;
        _options = options;
    }

    /// <summary>
    /// Every registered command, hidden ones included — they exist even though they
    /// are never listed. Read straight off the command service so it can never drift
    /// the way a hand-maintained constant does.
    /// </summary>
    public int TotalCommands => _commands.Commands.Count();

    /// <summary>
    /// How many of <see cref="TotalCommands"/> this user may run right now. Outside a
    /// server there are no guild permissions, so only the commands open to everyone
    /// count — which matches how every gated command answers "Server Only" in a DM.
    /// </summary>
    public int CountAvailable(IUser user)
    {
        var isOwner = _options.OwnerId is { } ownerId && user.Id == ownerId;
        var permissions = (user as IGuildUser)?.GuildPermissions;
        var available = 0;

        foreach (var command in _commands.Commands)
        {
            if (OwnerOnly.Contains(command.Name))
            {
                if (isOwner)
                    available++;

                continue;
            }

            // An unmapped command counts as open so the total is never under-reported;
            // Validate() is what surfaces the gap.
            if (!Gates.TryGetValue(command.Name, out var gate) || gate is null)
            {
                available++;
                continue;
            }

            if (permissions is { } guildPermissions && gate(guildPermissions))
                available++;
        }

        return available;
    }

    /// <summary>
    /// Startup drift guard: names a command that was added without an access rule.
    /// Silent when everything is mapped. Runs after the modules are loaded, so the
    /// command service already knows every command.
    /// </summary>
    public void Validate()
    {
        var missing = _commands.Commands
            .Select(command => command.Name)
            .Where(name =>
                !Gates.ContainsKey(name) &&
                !OwnerOnly.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (missing.Length == 0)
            return;

        Console.WriteLine(
            $"[Access] {missing.Length} command(s) have no access rule: " +
            string.Join(", ", missing));
    }

    private static Dictionary<string, Func<GuildPermissions, bool>?> BuildGates()
    {
        var gates = new Dictionary<string, Func<GuildPermissions, bool>?>(
            StringComparer.OrdinalIgnoreCase);

        // Open to every member. The ticket actions sit here because they carry no
        // command-level gate — TicketService checks the guild's support roles itself,
        // and a member may always close or claim their own ticket. Music is here for
        // the same reason: its only condition is being in the bot's voice channel,
        // which the module checks per call.
        Add(gates, null,
            "afk", "help", "ping", "membercount", "avatar", "banner", "roleinfo",
            "serverinfo", "stats", "userinfo", "rank", "vcrank", "leaderboard",
            "vclb", "vclist", "glist", "gentries", "setuproleshow", "new", "close",
            "reopen", "ticketdelete", "claim", "unclaim", "ticketadd", "ticketremove",
            "ticketrename", "transcript",
            "play", "pause", "resume", "skip", "stop", "queue", "nowplaying",
            "volume", "seek", "loop", "shuffle", "remove", "clear", "join", "leave");

        // Server configuration: AutoMod, leveling, tickets, logging, welcome,
        // giveaways, setup-roles, autoresponder, media and the prefix itself.
        Add(gates, ManageGuild,
            "automod", "anticaps", "antiduplicate", "antiemoji", "antiinvite",
            "antilink", "antimention", "antispam", "automodbypass", "automodlog",
            "badwords",
            "levelenable", "leveldisable", "levelconfig", "setcooldown", "setxprate",
            "setrankchannel", "setlevelupmessage", "togglelevelup", "levelrole",
            "blacklistchannel", "blacklistrole", "addxp", "removexp", "setlevel",
            "rankreset", "vcreset", "vcresetall",
            "ticket", "ticketsetup", "ticketcategory", "ticketlogs", "ticketrole",
            "ticketmessage", "ticketpanel", "ticketlimit", "ticketlist",
            "logconfig", "logset", "logenable", "logdisable", "logreset",
            "welcome", "welcomechannel", "welcomemessage",
            "gstart", "gend", "greroll",
            "setuprole", "setuprolecreate",
            "autoresponder", "media", "setprefix");

        Add(gates, ManageChannels,
            "lock", "unlock", "lockall", "unlockall", "hide", "unhide", "hideall",
            "unhideall", "vclock", "vcunlock", "vchide", "vcunhide");

        Add(gates, MoveMembers,
            "vckick", "vckickall", "vcmove", "vcmoveall", "vcpull", "vcpullall");

        Add(gates, MuteMembers,
            "vcmute", "vcunmute", "vcmuteall", "vcunmuteall");

        Add(gates, DeafenMembers,
            "vcdeafen", "vcundeafen", "vcdeafenall", "vcundeafenall");

        Add(gates, ManageRoles, "addrole", "autorole", "vcrole", "roleicon");
        Add(gates, BanMembers, "ban", "unban", "banlist");
        Add(gates, ModerateMembers, "mute", "unmute", "warn");
        Add(gates, ManageMessages, "purge", "snipe");
        Add(gates, ManageEmojis, "steal", "deleteemoji");
        Add(gates, KickMembers, "kick");
        Add(gates, ManageNicknames, "nick");
        Add(gates, Administrator, "nuke");

        return gates;
    }

    private static void Add(
        Dictionary<string, Func<GuildPermissions, bool>?> gates,
        Func<GuildPermissions, bool>? gate,
        params string[] names)
    {
        foreach (var name in names)
        {
            gates[name] = gate;
        }
    }

    // Each predicate mirrors the inline gate of the commands it covers, right down to
    // the `|| Administrator` fallback every one of them allows.

    private static bool ManageGuild(GuildPermissions permissions)
    {
        return permissions.ManageGuild || permissions.Administrator;
    }

    private static bool ManageChannels(GuildPermissions permissions)
    {
        return permissions.ManageChannels || permissions.Administrator;
    }

    private static bool ManageRoles(GuildPermissions permissions)
    {
        return permissions.ManageRoles || permissions.Administrator;
    }

    private static bool ManageMessages(GuildPermissions permissions)
    {
        return permissions.ManageMessages || permissions.Administrator;
    }

    private static bool ManageNicknames(GuildPermissions permissions)
    {
        return permissions.ManageNicknames || permissions.Administrator;
    }

    private static bool ManageEmojis(GuildPermissions permissions)
    {
        return permissions.ManageEmojisAndStickers || permissions.Administrator;
    }

    private static bool BanMembers(GuildPermissions permissions)
    {
        return permissions.BanMembers || permissions.Administrator;
    }

    private static bool KickMembers(GuildPermissions permissions)
    {
        return permissions.KickMembers || permissions.Administrator;
    }

    private static bool ModerateMembers(GuildPermissions permissions)
    {
        return permissions.ModerateMembers || permissions.Administrator;
    }

    private static bool MuteMembers(GuildPermissions permissions)
    {
        return permissions.MuteMembers || permissions.Administrator;
    }

    private static bool DeafenMembers(GuildPermissions permissions)
    {
        return permissions.DeafenMembers || permissions.Administrator;
    }

    private static bool MoveMembers(GuildPermissions permissions)
    {
        return permissions.MoveMembers || permissions.Administrator;
    }

    private static bool Administrator(GuildPermissions permissions)
    {
        return permissions.Administrator;
    }
}
