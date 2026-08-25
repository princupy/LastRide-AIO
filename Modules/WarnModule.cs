using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Services;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed class WarnModule : ModuleBase<SocketCommandContext>
{
    private const string DefaultReason = "No reason provided.";

    private readonly WarnComponentBuilder _builder;
    private readonly WarnService _warnService;

    public WarnModule(
        WarnComponentBuilder builder,
        WarnService warnService)
    {
        _builder = builder;
        _warnService = warnService;
    }

    [Command("warn")]
    [Summary("Warns a member and records the warning.")]
    public async Task WarnAsync([Remainder] string? input = null)
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
            !HasModeratePermission(moderator.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "You need `Moderate Members` or `Administrator` permission to use this command.");
            return;
        }

        var parsed = ParseInput(input);

        if (parsed is null)
        {
            await ReplyNoticeAsync(
                "Invalid Usage",
                "Usage: `?warn @user reason` or `?warn user_id reason`.");
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

        var hierarchyError = ValidateHierarchy(
            Context.Guild,
            moderator,
            target);

        if (hierarchyError is not null)
        {
            await ReplyNoticeAsync("Cannot Warn", hierarchyError);
            return;
        }

        var (warning, count) = _warnService.AddWarning(
            Context.Guild.Id,
            target.Id,
            moderator.Id,
            parsed.Value.Reason);

        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildWarned(
                target.Id,
                target.DisplayName,
                target.GetDisplayAvatarUrl(size: 256),
                warning.Reason,
                count,
                moderator.Id));
    }

    private async Task ReplyNoticeAsync(string title, string message)
    {
        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildNotice(title, message));
    }

    private SocketGuildUser? ResolveTarget(string query)
    {
        if (MentionUtils.TryParseUser(query, out var mentionedUserId) ||
            ulong.TryParse(query, out mentionedUserId))
        {
            return Context.Guild.GetUser(mentionedUserId);
        }

        var guildUser = Context.Guild.Users.FirstOrDefault(user =>
            user.Username.Equals(query, StringComparison.OrdinalIgnoreCase) ||
            user.DisplayName.Equals(query, StringComparison.OrdinalIgnoreCase));

        guildUser ??= Context.Guild.Users.FirstOrDefault(user =>
            user.Username.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            user.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase));

        return guildUser;
    }

    private static string? ValidateHierarchy(
        SocketGuild guild,
        SocketGuildUser moderator,
        SocketGuildUser target)
    {
        if (target.Id == moderator.Id)
            return "You cannot warn yourself.";

        if (target.Id == guild.CurrentUser.Id)
            return "I cannot be warned.";

        if (target.Id == guild.OwnerId)
            return "The server owner cannot be warned.";

        if (moderator.Id != guild.OwnerId &&
            target.Hierarchy >= moderator.Hierarchy)
        {
            return "You cannot warn a member with an equal or higher role.";
        }

        return null;
    }

    private static bool HasModeratePermission(GuildPermissions permissions)
    {
        return permissions.ModerateMembers || permissions.Administrator;
    }

    private static ParsedWarnInput? ParseInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var trimmed = input.Trim();
        var firstSpace = trimmed.IndexOf(' ');

        if (firstSpace < 0)
            return new ParsedWarnInput(trimmed, DefaultReason);

        var target = trimmed[..firstSpace].Trim();
        var reason = trimmed[(firstSpace + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(target))
            return null;

        return new ParsedWarnInput(
            target,
            string.IsNullOrWhiteSpace(reason) ? DefaultReason : reason);
    }

    private readonly record struct ParsedWarnInput(
        string Target,
        string Reason);
}
