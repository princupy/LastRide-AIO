using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Core;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed class AddRoleModule : ModuleBase<SocketCommandContext>
{
    private readonly AddRoleComponentBuilder _builder;

    public AddRoleModule(AddRoleComponentBuilder builder)
    {
        _builder = builder;
    }

    [Command("addrole")]
    [Alias("giverole", "role")]
    [Summary("Gives a role to a member.")]
    public async Task AddRoleAsync([Remainder] string? input = null)
    {
        if (Context.Guild is null)
        {
            await ReplyNoticeAsync(
                "Server Only",
                "This command can only be used in a server.");
            return;
        }

        var moderator = Context.User as SocketGuildUser;

        if (moderator is null ||
            !HasManageRoles(moderator.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "You need `Manage Roles` or `Administrator` permission to use this command.");
            return;
        }

        if (!HasManageRoles(Context.Guild.CurrentUser.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Bot Permission",
                "I need `Manage Roles` or `Administrator` permission to assign roles.");
            return;
        }

        var parsed = ParseInput(input);

        if (parsed is null)
        {
            await ReplyNoticeAsync(
                "Invalid Usage",
                "Usage: `?addrole @user role_id` or `?addrole user_id Role Name`.");
            return;
        }

        var target = ResolveTarget(parsed.Value.Target);

        if (target is null)
        {
            await ReplyNoticeAsync(
                "User Not Found",
                "I could not find that member. Mention them or provide a valid user ID.");
            return;
        }

        if (!TryResolveRole(Context.Guild, parsed.Value.Role, out var role))
        {
            await ReplyNoticeAsync(
                "Role Not Found",
                "I could not find that role. Provide a role ID, mention, or name.");
            return;
        }

        var roleError = ValidateRole(Context.Guild, moderator, role);

        if (roleError is not null)
        {
            await ReplyNoticeAsync("Cannot Add Role", roleError);
            return;
        }

        if (target.Roles.Any(existing => existing.Id == role.Id))
        {
            await ReplyAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildAlreadyHasRole(
                    target.Id,
                    role.Name,
                    role.Id,
                    moderator.Id,
                    Context.Guild.Id));
            return;
        }

        try
        {
            await target.AddRoleAsync(
                role,
                new RequestOptions
                {
                    AuditLogReason = $"Role added by {moderator.Username}"
                });

            await ReplyAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildSuccess(
                    target.Id,
                    target.DisplayName,
                    target.GetDisplayAvatarUrl(size: 256),
                    role.Name,
                    role.Id,
                    moderator.Id));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[AddRole Error] {DiscordFailure.Format(exception)}");

            await ReplyNoticeAsync(
                "Role Add Failed",
                DiscordFailure.Describe(
                    exception,
                    "I could not add that role. Check my permissions and role position."));
        }
    }

    private async Task ReplyNoticeAsync(string title, string message)
    {
        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildNotice(title, message));
    }

    /// <summary>
    /// Only an explicit reference counts — see <see cref="UserReference"/> for why a plain
    /// name is refused rather than matched.
    /// </summary>
    private SocketGuildUser? ResolveTarget(string query)
    {
        return UserReference.TryParse(query, out var userId)
            ? Context.Guild.GetUser(userId)
            : null;
    }

    private static bool TryResolveRole(
        SocketGuild guild,
        string query,
        out SocketRole role)
    {
        role = null!;

        if (string.IsNullOrWhiteSpace(query))
            return false;

        query = query.Trim();

        if (MentionUtils.TryParseRole(query, out var mentionedRoleId) ||
            ulong.TryParse(query, out mentionedRoleId))
        {
            var resolvedRole = guild.GetRole(mentionedRoleId);

            if (resolvedRole is null)
                return false;

            role = resolvedRole;
            return true;
        }

        var exactRole = guild.Roles.FirstOrDefault(candidate =>
            candidate.Name.Equals(
                query,
                StringComparison.OrdinalIgnoreCase));

        if (exactRole is not null)
        {
            role = exactRole;
            return true;
        }

        // A partial name is only trusted when exactly one role can match it. Taking the
        // first of several used to hand out whichever role the list happened to yield —
        // "mod" could land on "Moderator" or "Mod Applicant" depending on role order.
        var partialMatches = guild.Roles
            .Where(candidate => candidate.Name.Contains(
                query,
                StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();

        if (partialMatches.Length != 1)
            return false;

        role = partialMatches[0];
        return true;
    }

    private static string? ValidateRole(
        SocketGuild guild,
        SocketGuildUser moderator,
        SocketRole role)
    {
        if (role.Id == guild.EveryoneRole.Id)
            return "The `@everyone` role cannot be assigned.";

        if (role.IsManaged)
            return "That role is managed by an integration and cannot be assigned.";

        if (moderator.Id != guild.OwnerId &&
            role.Position >= moderator.Hierarchy)
        {
            return "You cannot assign a role that is equal to or higher than your highest role.";
        }

        if (role.Position >= guild.CurrentUser.Hierarchy)
            return "My highest role must be above the role you want to assign.";

        return null;
    }

    private static bool HasManageRoles(GuildPermissions permissions)
    {
        return permissions.ManageRoles || permissions.Administrator;
    }

    private static ParsedAddRoleInput? ParseInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var trimmed = input.Trim();
        var separatorIndex = trimmed.IndexOf(' ');

        if (separatorIndex < 0)
            return null;

        var target = trimmed[..separatorIndex].Trim();
        var role = trimmed[(separatorIndex + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(target) ||
            string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        return new ParsedAddRoleInput(target, role);
    }

    private static string EscapeInlineCode(string value)
    {
        return value.Replace("`", "'");
    }

    private readonly record struct ParsedAddRoleInput(
        string Target,
        string Role);
}
