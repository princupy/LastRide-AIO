using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Core;
using LastRide.Services;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed class BanModule : ModuleBase<SocketCommandContext>
{
    private const string DefaultReason = "No reason provided.";
    private readonly BanComponentBuilder _builder;
    private readonly BanConfirmationService _confirmationService;

    public BanModule(
        BanComponentBuilder builder,
        BanConfirmationService confirmationService)
    {
        _builder = builder;
        _confirmationService = confirmationService;
    }

    [Command("ban")]
    [Summary("Bans a user after confirmation.")]
    public async Task BanAsync([Remainder] string? input = null)
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
            !HasBanPermission(moderator.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "You need `Ban Members` or `Administrator` permission to use this command.");
            return;
        }

        if (!HasBanPermission(Context.Guild.CurrentUser.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Bot Permission",
                "I need `Ban Members` or `Administrator` permission to ban users.");
            return;
        }

        var parsed = ParseInput(input);

        if (parsed is null)
        {
            await ReplyNoticeAsync(
                "Invalid Usage",
                "Usage: `?ban @user reason` or `?ban user_id reason`.");
            return;
        }

        var target = await ResolveTargetAsync(parsed.Value.Target);

        if (target is null)
        {
            await ReplyNoticeAsync(
                "User Not Found",
                "I could not find that user. Mention them or provide a valid user ID.");
            return;
        }

        var hierarchyError = ValidateHierarchy(
            Context.Guild,
            moderator,
            target);

        if (hierarchyError is not null)
        {
            await ReplyNoticeAsync("Cannot Ban", hierarchyError);
            return;
        }

        var request = _confirmationService.Create(
            Context.Guild.Id,
            Context.User.Id,
            target.Id,
            GetDisplayName(target),
            target.GetDisplayAvatarUrl(size: 256),
            string.IsNullOrWhiteSpace(parsed.Value.Reason)
                ? DefaultReason
                : parsed.Value.Reason);

        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildPrompt(request));
    }

    private async Task ReplyNoticeAsync(string title, string message)
    {
        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildNotice(title, message));
    }

    /// <summary>
    /// Only an explicit reference counts — see <see cref="UserReference"/> for why a plain
    /// name is refused rather than matched. The REST lookup stays so a member who already
    /// left, or was never cached, can still be banned by ID.
    /// </summary>
    private async Task<IUser?> ResolveTargetAsync(string query)
    {
        if (!UserReference.TryParse(query, out var userId))
            return null;

        return Context.Guild.GetUser(userId) as IUser ??
            await Context.Client.Rest.GetUserAsync(userId);
    }

    private static string? ValidateHierarchy(
        SocketGuild guild,
        SocketGuildUser moderator,
        IUser target)
    {
        if (target.Id == moderator.Id)
            return "You cannot ban yourself.";

        if (target.Id == guild.CurrentUser.Id)
            return "I cannot ban myself.";

        if (target.Id == guild.OwnerId)
            return "The server owner cannot be banned.";

        var targetMember = guild.GetUser(target.Id);

        if (targetMember is null)
            return null;

        if (moderator.Id != guild.OwnerId &&
            targetMember.Hierarchy >= moderator.Hierarchy)
        {
            return "You cannot ban a member with an equal or higher role.";
        }

        if (targetMember.Hierarchy >= guild.CurrentUser.Hierarchy)
            return "My highest role must be above the target member's highest role.";

        return null;
    }

    private static bool HasBanPermission(GuildPermissions permissions)
    {
        return permissions.BanMembers || permissions.Administrator;
    }

    private static ParsedBanInput? ParseInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var trimmed = input.Trim();
        var separatorIndex = trimmed.IndexOf(' ');

        if (separatorIndex < 0)
        {
            return new ParsedBanInput(trimmed, DefaultReason);
        }

        var target = trimmed[..separatorIndex].Trim();
        var reason = trimmed[(separatorIndex + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(target))
            return null;

        return new ParsedBanInput(
            target,
            string.IsNullOrWhiteSpace(reason)
                ? DefaultReason
                : reason);
    }

    private static string GetDisplayName(IUser user)
    {
        if (user is SocketGuildUser guildUser)
            return guildUser.DisplayName;

        if (user is RestUser restUser &&
            !string.IsNullOrWhiteSpace(restUser.GlobalName))
        {
            return restUser.GlobalName;
        }

        return string.IsNullOrWhiteSpace(user.GlobalName)
            ? user.Username
            : user.GlobalName;
    }

    private readonly record struct ParsedBanInput(
        string Target,
        string Reason);
}
