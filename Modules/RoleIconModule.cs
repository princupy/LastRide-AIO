using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Core;
using LastRide.Services;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed class RoleIconModule : ModuleBase<SocketCommandContext>
{
    private readonly RoleIconComponentBuilder _builder;
    private readonly RoleIconService _iconService;

    public RoleIconModule(
        RoleIconComponentBuilder builder,
        RoleIconService iconService)
    {
        _builder = builder;
        _iconService = iconService;
    }

    [Command("roleicon")]
    [Alias("seticon", "ricon")]
    [Summary("Sets a server emoji as the icon for a role.")]
    public async Task RoleIconAsync([Remainder] string? input = null)
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
                "I need `Manage Roles` or `Administrator` permission to modify roles.");
            return;
        }

        if (!Context.Guild.Features.HasFeature(GuildFeature.RoleIcons))
        {
            await ReplyNoticeAsync(
                "Feature Not Available",
                "Role icons require your server to be **Level 2** boosted or higher.");
            return;
        }

        var parsed = ParseInput(input);

        if (parsed is null)
        {
            await ReplyNoticeAsync(
                "Invalid Usage",
                "Usage: `?roleicon role_id <:emoji:id>` or `?roleicon Role Name <:emoji:id>`.");
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
            await ReplyNoticeAsync("Cannot Modify Role", roleError);
            return;
        }

        if (!TryResolveEmote(Context.Guild, parsed.Value.Emoji, out var emote))
        {
            await ReplyNoticeAsync(
                "Emoji Not Found",
                "I could not find that emoji. Use a server emoji like `<:name:id>`.");
            return;
        }

        var icon = await _iconService.DownloadIconAsync(emote.Url);

        if (icon is null)
        {
            await ReplyNoticeAsync(
                "Icon Download Failed",
                "I could not download that emoji image. Try a different emoji.");
            return;
        }

        try
        {
            using var iconImage = icon.Value;

            await role.ModifyAsync(
                properties => properties.Icon = iconImage,
                new RequestOptions
                {
                    AuditLogReason =
                        $"Role icon set by {moderator.Username}"
                });

            await ReplyAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildSuccess(
                    role.Name,
                    role.Id,
                    emote.ToString(),
                    emote.Url,
                    moderator.Id));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[RoleIcon Error] {DiscordFailure.Format(exception)}");

            await ReplyNoticeAsync(
                "Role Icon Failed",
                DiscordFailure.Describe(
                    exception,
                    "I could not set the role icon. Check my permissions and role position."));
        }
    }

    private async Task ReplyNoticeAsync(string title, string message)
    {
        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildNotice(title, message));
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

        // A partial name is only trusted when exactly one role can match it — see
        // AddRoleModule for the ordering problem this avoids.
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

    private static bool TryResolveEmote(
        SocketGuild guild,
        string query,
        out GuildEmote emote)
    {
        emote = null!;

        if (string.IsNullOrWhiteSpace(query))
            return false;

        query = query.Trim();

        // Try parsing <:name:id> or <a:name:id> format
        if (Emote.TryParse(query, out var parsedEmote))
        {
            var guildEmote = guild.Emotes.FirstOrDefault(
                e => e.Id == parsedEmote.Id);

            if (guildEmote is null)
                return false;

            emote = guildEmote;
            return true;
        }

        // Try matching by name
        var byName = guild.Emotes.FirstOrDefault(e =>
            e.Name.Equals(
                query.Trim(':'),
                StringComparison.OrdinalIgnoreCase));

        if (byName is null)
            return false;

        emote = byName;
        return true;
    }

    private static string? ValidateRole(
        SocketGuild guild,
        SocketGuildUser moderator,
        SocketRole role)
    {
        if (role.Id == guild.EveryoneRole.Id)
            return "The `@everyone` role cannot be modified.";

        if (role.IsManaged)
            return "That role is managed by an integration and cannot be modified.";

        if (moderator.Id != guild.OwnerId &&
            role.Position >= moderator.Hierarchy)
        {
            return "You cannot modify a role that is equal to or higher than your highest role.";
        }

        if (role.Position >= guild.CurrentUser.Hierarchy)
            return "My highest role must be above the role you want to modify.";

        return null;
    }

    private static bool HasManageRoles(GuildPermissions permissions)
    {
        return permissions.ManageRoles || permissions.Administrator;
    }

    private static ParsedRoleIconInput? ParseInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var trimmed = input.Trim();

        // Last token is the emoji — everything before it is the role
        var lastSpace = trimmed.LastIndexOf(' ');

        if (lastSpace < 0)
            return null;

        var rolePart = trimmed[..lastSpace].Trim();
        var emojiPart = trimmed[(lastSpace + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(rolePart) ||
            string.IsNullOrWhiteSpace(emojiPart))
        {
            return null;
        }

        return new ParsedRoleIconInput(rolePart, emojiPart);
    }

    private readonly record struct ParsedRoleIconInput(
        string Role,
        string Emoji);
}
