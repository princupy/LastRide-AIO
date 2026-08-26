using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Services;

namespace LastRide.Modules;

[Name("AutoRole")]
public sealed class AutoRoleModule : ModuleBase<SocketCommandContext>
{
    private readonly AutoRoleConfigService _configService;
    private readonly AutoRoleComponentBuilder _builder;
    private readonly PrefixService _prefixService;

    public AutoRoleModule(
        AutoRoleConfigService configService,
        AutoRoleComponentBuilder builder,
        PrefixService prefixService)
    {
        _configService = configService;
        _builder = builder;
        _prefixService = prefixService;
    }

    [Command("autorole")]
    [Alias("autoroles")]
    [Summary("Automatically assigns roles to members and bots when they join.")]
    public async Task AutoRoleAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);
        var prefix = Prefix;

        if (parts.Length == 0 ||
            parts[0].Equals("list", StringComparison.OrdinalIgnoreCase) ||
            parts[0].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            await ReplyAutoRoleStatusAsync(note: null);
            return;
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "on":
            case "enable":
                await _configService.SetAutoRoleEnabledAsync(Context.Guild.Id, true);
                await ReplyAutoRoleStatusAsync("Autorole enabled.");
                break;
            case "off":
            case "disable":
                await _configService.SetAutoRoleEnabledAsync(Context.Guild.Id, false);
                await ReplyAutoRoleStatusAsync("Autorole disabled.");
                break;
            case "reset":
                await _configService.ResetAutoRolesAsync(Context.Guild.Id);
                await ReplyAutoRoleStatusAsync("Autorole configuration reset.");
                break;
            case "add":
                await HandleAutoRoleAddAsync(parts, AutoRoleTarget.All);
                break;
            case "humans":
            case "human":
                await HandleAutoRoleAddAsync(parts, AutoRoleTarget.Humans);
                break;
            case "bots":
            case "bot":
                await HandleAutoRoleAddAsync(parts, AutoRoleTarget.Bots);
                break;
            case "remove":
            case "delete":
            case "del":
                await HandleAutoRoleRemoveAsync(parts);
                break;
            default:
                await ReplyNoticeAsync(
                    "Invalid Usage",
                    $"Usage: `{prefix}autorole add @role`, `{prefix}autorole humans @role`, `{prefix}autorole bots @role`, `{prefix}autorole remove @role`, `{prefix}autorole list`, `{prefix}autorole on/off`, `{prefix}autorole reset`.");
                break;
        }
    }

    [Command("vcrole")]
    [Alias("voicerole")]
    [Summary("Gives members a role while they are in a voice channel.")]
    public async Task VcRoleAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);
        var prefix = Prefix;

        if (parts.Length == 0 ||
            parts[0].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            await ReplyVcRoleStatusAsync(note: null);
            return;
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "on":
            case "enable":
                await _configService.SetVcRoleEnabledAsync(Context.Guild.Id, true);
                await ReplyVcRoleStatusAsync("Voice role enabled.");
                break;
            case "off":
            case "disable":
                await _configService.SetVcRoleEnabledAsync(Context.Guild.Id, false);
                await ReplyVcRoleStatusAsync("Voice role disabled.");
                break;
            case "reset":
                await _configService.ResetVcRoleAsync(Context.Guild.Id);
                await ReplyVcRoleStatusAsync("Voice role configuration reset.");
                break;
            case "remove":
            case "clear":
            case "delete":
            case "del":
                await _configService.SetVcRoleAsync(Context.Guild.Id, null);
                await ReplyVcRoleStatusAsync("Voice role cleared.");
                break;
            case "set":
                await HandleVcRoleSetAsync(parts);
                break;
            default:
                await ReplyNoticeAsync(
                    "Invalid Usage",
                    $"Usage: `{prefix}vcrole set @role`, `{prefix}vcrole remove`, `{prefix}vcrole status`, `{prefix}vcrole on/off`, `{prefix}vcrole reset`.");
                break;
        }
    }

    private async Task HandleAutoRoleAddAsync(string[] parts, AutoRoleTarget target)
    {
        if (parts.Length < 2 || !TryResolveRole(parts[1], out var role))
        {
            await ReplyNoticeAsync(
                "Role Not Found",
                "Mention a role or provide a valid role ID.");
            return;
        }

        var validationError = ValidateAssignableRole(role);

        if (validationError is not null)
        {
            await ReplyNoticeAsync("Invalid Role", validationError);
            return;
        }

        var result = await _configService.AddAutoRoleAsync(
            Context.Guild.Id, role.Id, target);

        var scope = target switch
        {
            AutoRoleTarget.Humans => "human",
            AutoRoleTarget.Bots => "bot",
            _ => "join"
        };

        switch (result.Result)
        {
            case RoleListResult.Added:
                await ReplyAutoRoleStatusAsync(
                    $"Added <@&{role.Id}> to the {scope} autorole list.");
                break;
            case RoleListResult.AlreadyPresent:
                await ReplyNoticeAsync(
                    "Already Added",
                    $"<@&{role.Id}> is already on the {scope} autorole list.");
                break;
            case RoleListResult.LimitReached:
                await ReplyNoticeAsync(
                    "Limit Reached",
                    $"You can have at most `{AutoRoleConfigService.MaxRolesPerType}` roles per list.");
                break;
        }
    }

    private async Task HandleAutoRoleRemoveAsync(string[] parts)
    {
        if (parts.Length < 2 || !TryResolveRole(parts[1], out var role))
        {
            await ReplyNoticeAsync(
                "Role Not Found",
                "Mention a role or provide a valid role ID.");
            return;
        }

        var result = await _configService.RemoveAutoRoleAsync(Context.Guild.Id, role.Id);

        switch (result.Result)
        {
            case RoleListResult.Removed:
                await ReplyAutoRoleStatusAsync(
                    $"Removed <@&{role.Id}> from the autorole lists.");
                break;
            case RoleListResult.NotPresent:
                await ReplyNoticeAsync(
                    "Not Found",
                    $"<@&{role.Id}> is not on any autorole list.");
                break;
        }
    }

    private async Task HandleVcRoleSetAsync(string[] parts)
    {
        if (parts.Length < 2 || !TryResolveRole(parts[1], out var role))
        {
            await ReplyNoticeAsync(
                "Role Not Found",
                "Mention a role or provide a valid role ID.");
            return;
        }

        var validationError = ValidateAssignableRole(role);

        if (validationError is not null)
        {
            await ReplyNoticeAsync("Invalid Role", validationError);
            return;
        }

        await _configService.SetVcRoleAsync(Context.Guild.Id, role.Id);
        await ReplyVcRoleStatusAsync($"Voice role set to <@&{role.Id}>.");
    }

    private Task ReplyAutoRoleStatusAsync(string? note)
    {
        var config = _configService.GetConfig(Context.Guild.Id);

        return ReplyComponentsAsync(_builder.BuildAutoRoleStatus(
            config.AutoRoleEnabled,
            config.HumanRoleIds.ToArray(),
            config.BotRoleIds.ToArray(),
            AutoRoleConfigService.MaxRolesPerType,
            note,
            _configService.IsPersistent,
            Prefix));
    }

    private Task ReplyVcRoleStatusAsync(string? note)
    {
        var config = _configService.GetConfig(Context.Guild.Id);

        return ReplyComponentsAsync(_builder.BuildVcRoleStatus(
            config.VcRoleEnabled,
            config.VcRoleId,
            note,
            _configService.IsPersistent,
            Prefix));
    }

    private string? ValidateAssignableRole(SocketRole role)
    {
        if (role.Id == Context.Guild.EveryoneRole.Id)
            return "The `@everyone` role cannot be assigned.";

        if (role.IsManaged)
            return "That role is managed by an integration and cannot be assigned.";

        if (role.Position >= Context.Guild.CurrentUser.Hierarchy)
            return "My highest role must be above the role you want to assign.";

        return null;
    }

    private bool TryResolveRole(string token, out SocketRole role)
    {
        role = null!;

        if (!MentionUtils.TryParseRole(token, out var roleId) &&
            !ulong.TryParse(token, out roleId))
        {
            return false;
        }

        var resolved = Context.Guild.GetRole(roleId);

        if (resolved is null)
            return false;

        role = resolved;
        return true;
    }

    private async Task<bool> EnsureAllowedAsync()
    {
        if (Context.Guild is null)
        {
            await ReplyNoticeAsync(
                "Server Only",
                "This command can only be used in a server.");
            return false;
        }

        if (Context.User is not SocketGuildUser user ||
            !(user.GuildPermissions.ManageRoles || user.GuildPermissions.Administrator))
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "You need `Manage Roles` or `Administrator` permission to manage auto-roles.");
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
        return ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: components);
    }

    private Task ReplyNoticeAsync(string title, string message)
    {
        return ReplyComponentsAsync(_builder.BuildNotice(title, message));
    }
}
