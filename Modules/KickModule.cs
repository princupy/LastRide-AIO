using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Core;
using LastRide.Services;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed class KickModule : ModuleBase<SocketCommandContext>
{
    private const string DefaultReason = "No reason provided.";
    private readonly KickComponentBuilder _builder;
    private readonly KickConfirmationService _confirmationService;

    public KickModule(
        KickComponentBuilder builder,
        KickConfirmationService confirmationService)
    {
        _builder = builder;
        _confirmationService = confirmationService;
    }

    [Command("kick")]
    [Summary("Kicks a user after confirmation.")]
    public async Task KickAsync([Remainder] string? input = null)
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
            !HasKickPermission(moderator.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "You need `Kick Members` or `Administrator` permission to use this command.");
            return;
        }

        if (!HasKickPermission(Context.Guild.CurrentUser.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Bot Permission",
                "I need `Kick Members` or `Administrator` permission to kick users.");
            return;
        }

        var parsed = ParseInput(input);

        if (parsed is null)
        {
            await ReplyNoticeAsync(
                "Invalid Usage",
                "Usage: `?kick @user reason` or `?kick user_id reason`.");
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
            await ReplyNoticeAsync("Cannot Kick", hierarchyError);
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
    /// name is refused rather than matched. The REST lookup stays so an uncached member can
    /// still be resolved by ID.
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
            return "You cannot kick yourself.";

        if (target.Id == guild.CurrentUser.Id)
            return "I cannot kick myself.";

        if (target.Id == guild.OwnerId)
            return "The server owner cannot be kicked.";

        var targetMember = guild.GetUser(target.Id);

        if (targetMember is null)
            return "That user is not in this server.";

        if (moderator.Id != guild.OwnerId &&
            targetMember.Hierarchy >= moderator.Hierarchy)
        {
            return "You cannot kick a member with an equal or higher role.";
        }

        if (targetMember.Hierarchy >= guild.CurrentUser.Hierarchy)
            return "My highest role must be above the target member's highest role.";

        return null;
    }

    private static bool HasKickPermission(GuildPermissions permissions)
    {
        return permissions.KickMembers || permissions.Administrator;
    }

    private static ParsedKickInput? ParseInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var trimmed = input.Trim();
        var separatorIndex = trimmed.IndexOf(' ');

        if (separatorIndex < 0)
        {
            return new ParsedKickInput(trimmed, DefaultReason);
        }

        var target = trimmed[..separatorIndex].Trim();
        var reason = trimmed[(separatorIndex + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(target))
            return null;

        return new ParsedKickInput(
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

    private readonly record struct ParsedKickInput(
        string Target,
        string Reason);
}
