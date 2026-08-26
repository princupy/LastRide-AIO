using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Services;

namespace LastRide.Modules;

[Name("Setup-Roles")]
public sealed class SetupRoleModule : ModuleBase<SocketCommandContext>
{
    private readonly SetupRoleConfigService _configService;
    private readonly SetupRoleComponentBuilder _builder;
    private readonly PrefixService _prefixService;
    private readonly CommandService _commands;

    public SetupRoleModule(
        SetupRoleConfigService configService,
        SetupRoleComponentBuilder builder,
        PrefixService prefixService,
        CommandService commands)
    {
        _configService = configService;
        _builder = builder;
        _prefixService = prefixService;
        _commands = commands;
    }

    [Command("setuprole")]
    [Alias("setuproles")]
    [Summary("Configure staff roles allowed to use dynamic role commands.")]
    public async Task SetupRoleAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);
        var action = parts.Length == 0
            ? string.Empty
            : parts[0].ToLowerInvariant();

        switch (action)
        {
            case "add":
            case "remove":
                await UpdateStaffRoleAsync(action, parts);
                return;

            case "list":
            case "show":
                await ReplyComponentsAsync(_builder.BuildStaffRoleList(
                    _configService.GetConfig(Context.Guild.Id),
                    Prefix,
                    _configService.IsPersistent));
                return;

            default:
                await ReplyStaffUsageAsync();
                return;
        }
    }

    [Command("setuprolecreate")]
    [Alias("setuprolecmd")]
    [Summary("Create, update, remove, or list dynamic role assignment commands.")]
    public async Task SetupRoleCreateAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);

        if (parts.Length == 0)
        {
            await ReplyCreateUsageAsync();
            return;
        }

        var action = parts[0].ToLowerInvariant();

        if (action is "list" or "show")
        {
            await ReplyCommandListAsync();
            return;
        }

        if (action is "remove" or "delete")
        {
            await RemoveRoleCommandAsync(parts);
            return;
        }

        await CreateRoleCommandAsync(parts);
    }

    [Command("setuproleshow")]
    [Alias("setuprolelist")]
    [Summary("Shows the role-assignment commands created with setuprolecreate.")]
    public async Task SetupRoleShowAsync()
    {
        if (!await EnsureGuildAsync())
            return;

        var config = _configService.GetConfig(Context.Guild.Id);

        // Staff who can actually run these commands may list them too, so they
        // don't have to guess which names exist.
        if (Context.User is not SocketGuildUser member ||
            !(member.GuildPermissions.ManageGuild ||
                member.GuildPermissions.Administrator ||
                config.HasStaffRole(member.Roles.Select(role => role.Id))))
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "You need `Manage Server` permission or one of the configured " +
                "staff roles to view the role commands.");

            return;
        }

        await ReplyComponentsAsync(_builder.BuildCommandList(
            config,
            Prefix,
            _configService.IsPersistent));
    }

    private async Task UpdateStaffRoleAsync(string action, string[] parts)
    {
        if (parts.Length < 2)
        {
            await ReplyStaffUsageAsync();
            return;
        }

        var query = string.Join(' ', parts.Skip(1));

        if (!TryResolveRole(query, out var role))
        {
            await ReplyNoticeAsync(
                "Role Not Found",
                $"I could not find a role matching {Inline(query)}.");

            return;
        }

        if (role.Id == Context.Guild.EveryoneRole.Id)
        {
            await ReplyNoticeAsync(
                "Invalid Role",
                "The `@everyone` role cannot be used as a staff role.");

            return;
        }

        var isAdd = action == "add";

        var update = isAdd
            ? await _configService.AddStaffRoleAsync(Context.Guild.Id, role.Id)
            : await _configService.RemoveStaffRoleAsync(Context.Guild.Id, role.Id);

        await (update.Result switch
        {
            SetupRoleResult.Added => ReplyResultAsync(
                "Staff Role Added",
                $"<@&{role.Id}> can now use the server's role commands.",
                update.Persisted),

            SetupRoleResult.Removed => ReplyResultAsync(
                "Staff Role Removed",
                $"<@&{role.Id}> can no longer use the server's role commands.",
                update.Persisted),

            SetupRoleResult.AlreadyPresent => ReplyNoticeAsync(
                "No Change",
                $"<@&{role.Id}> is already a configured staff role."),

            SetupRoleResult.NotPresent => ReplyNoticeAsync(
                "No Change",
                $"<@&{role.Id}> is not a configured staff role."),

            SetupRoleResult.LimitReached => ReplyNoticeAsync(
                "Limit Reached",
                $"This server already has `{SetupRoleConfigService.MaxStaffRoles}` " +
                "staff roles configured."),

            _ => ReplyNoticeAsync(
                "Invalid Input",
                "That role could not be saved.")
        });
    }

    private async Task CreateRoleCommandAsync(string[] parts)
    {
        if (parts.Length < 2)
        {
            await ReplyCreateUsageAsync();
            return;
        }

        var name = SetupRoleConfigService.NormalizeCommandName(parts[0]);

        if (!SetupRoleConfigService.IsValidCommandName(name))
        {
            await ReplyNoticeAsync(
                "Invalid Name",
                $"Command names must be `{SetupRoleConfigService.MinCommandNameLength}`-" +
                $"`{SetupRoleConfigService.MaxCommandNameLength}` characters long and " +
                "may only contain letters, numbers, `-` or `_`.");

            return;
        }

        // A dynamic name that matches a built-in command would never run, since
        // the built-in command always wins the dispatch.
        if (IsReservedName(name))
        {
            await ReplyNoticeAsync(
                "Name Taken",
                $"{Inline($"{Prefix}{name}")} is already a built-in command. " +
                "Pick a different name.");

            return;
        }

        var query = string.Join(' ', parts.Skip(1));

        if (!TryResolveRole(query, out var role))
        {
            await ReplyNoticeAsync(
                "Role Not Found",
                $"I could not find a role matching {Inline(query)}.");

            return;
        }

        var problem = ValidateRoleForCreate(role);

        if (problem is not null)
        {
            await ReplyNoticeAsync("Cannot Use Role", problem);
            return;
        }

        var update = await _configService.SetCommandAsync(
            Context.Guild.Id,
            name,
            role.Id);

        var usage = $"`{Prefix}{name} @user`";

        await (update.Result switch
        {
            SetupRoleResult.Added => ReplyResultAsync(
                "Role Command Created",
                $"{usage} now toggles <@&{role.Id}> — the role is added when the " +
                "member does not have it and removed when they do.",
                update.Persisted),

            SetupRoleResult.Updated => ReplyResultAsync(
                "Role Command Updated",
                $"{usage} now toggles <@&{role.Id}> instead.",
                update.Persisted),

            SetupRoleResult.LimitReached => ReplyNoticeAsync(
                "Limit Reached",
                $"This server already has `{SetupRoleConfigService.MaxCommands}` " +
                "role commands."),

            _ => ReplyNoticeAsync(
                "Invalid Input",
                "That command could not be saved.")
        });
    }

    private async Task RemoveRoleCommandAsync(string[] parts)
    {
        if (parts.Length < 2)
        {
            await ReplyCreateUsageAsync();
            return;
        }

        var name = SetupRoleConfigService.NormalizeCommandName(parts[1]);

        if (name.Length == 0)
        {
            await ReplyCreateUsageAsync();
            return;
        }

        var update = await _configService.RemoveCommandAsync(Context.Guild.Id, name);

        await (update.Result switch
        {
            SetupRoleResult.Removed => ReplyResultAsync(
                "Role Command Removed",
                $"{Inline($"{Prefix}{name}")} no longer assigns a role.",
                update.Persisted),

            _ => ReplyNoticeAsync(
                "No Change",
                $"{Inline($"{Prefix}{name}")} is not a role command. " +
                $"Check `{Prefix}setuproleshow`.")
        });
    }

    private Task ReplyCommandListAsync()
    {
        return ReplyComponentsAsync(_builder.BuildCommandList(
            _configService.GetConfig(Context.Guild.Id),
            Prefix,
            _configService.IsPersistent));
    }

    private Task ReplyStaffUsageAsync()
    {
        return ReplyNoticeAsync(
            "Setup Role Usage",
            $"`{Prefix}setuprole add @role` • `{Prefix}setuprole remove @role` • " +
            $"`{Prefix}setuprole list`");
    }

    private Task ReplyCreateUsageAsync()
    {
        return ReplyNoticeAsync(
            "Setup Role Create Usage",
            $"`{Prefix}setuprolecreate <name> @role` • " +
            $"`{Prefix}setuprolecreate remove <name>` • " +
            $"`{Prefix}setuprolecreate list`");
    }

    private string? ValidateRoleForCreate(SocketRole role)
    {
        if (role.Id == Context.Guild.EveryoneRole.Id)
            return "The `@everyone` role cannot be assigned.";

        if (role.IsManaged)
            return "That role is managed by an integration and cannot be assigned.";

        if (Context.User is SocketGuildUser creator &&
            creator.Id != Context.Guild.OwnerId &&
            role.Position >= creator.Hierarchy)
        {
            return "You cannot create a command for a role that is equal to or " +
                "higher than your highest role.";
        }

        if (role.Position >= Context.Guild.CurrentUser.Hierarchy)
            return "My highest role must be above the role you want to assign.";

        return null;
    }

    private bool IsReservedName(string name)
    {
        return _commands.Commands.Any(command =>
            command.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            command.Aliases.Any(alias =>
                alias.Equals(name, StringComparison.OrdinalIgnoreCase)));
    }

    private bool TryResolveRole(string query, out SocketRole role)
    {
        role = null!;

        if (string.IsNullOrWhiteSpace(query))
            return false;

        query = query.Trim();

        if (MentionUtils.TryParseRole(query, out var roleId) ||
            ulong.TryParse(query, out roleId))
        {
            var resolvedRole = Context.Guild.GetRole(roleId);

            if (resolvedRole is null)
                return false;

            role = resolvedRole;
            return true;
        }

        var exactRole = Context.Guild.Roles.FirstOrDefault(candidate =>
            candidate.Name.Equals(query, StringComparison.OrdinalIgnoreCase));

        if (exactRole is not null)
        {
            role = exactRole;
            return true;
        }

        var partialRole = Context.Guild.Roles.FirstOrDefault(candidate =>
            candidate.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        if (partialRole is null)
            return false;

        role = partialRole;
        return true;
    }

    private async Task<bool> EnsureGuildAsync()
    {
        if (Context.Guild is not null)
            return true;

        await ReplyNoticeAsync("Server Only", "This command can only be used in a server.");
        return false;
    }

    private async Task<bool> EnsureAllowedAsync()
    {
        if (!await EnsureGuildAsync())
            return false;

        if (Context.User is not SocketGuildUser user ||
            !(user.GuildPermissions.ManageGuild || user.GuildPermissions.Administrator))
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "You need `Manage Server` or `Administrator` permission to manage " +
                "role commands.");

            return false;
        }

        return true;
    }

    private string Prefix => _prefixService.GetPrefix(Context.Guild?.Id);

    private static string[] Split(string? input)
    {
        return string.IsNullOrWhiteSpace(input)
            ? Array.Empty<string>()
            : input.Trim().Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private Task ReplyComponentsAsync(MessageComponent components)
    {
        return ReplyAsync(allowedMentions: AllowedMentions.None, components: components);
    }

    private Task ReplyResultAsync(string title, string message, bool persisted)
    {
        return ReplyComponentsAsync(_builder.BuildResult(title, message, persisted));
    }

    private Task ReplyNoticeAsync(string title, string message)
    {
        return ReplyComponentsAsync(_builder.BuildNotice(title, message));
    }

    private static string Inline(string value)
    {
        return $"`{value.Replace("`", "'")}`";
    }
}
