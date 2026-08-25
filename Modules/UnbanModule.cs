using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Services;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed class UnbanModule : ModuleBase<SocketCommandContext>
{
    private const string DefaultReason = "No reason provided.";
    private readonly UnbanComponentBuilder _builder;
    private readonly UnbanConfirmationService _confirmationService;

    public UnbanModule(
        UnbanComponentBuilder builder,
        UnbanConfirmationService confirmationService)
    {
        _builder = builder;
        _confirmationService = confirmationService;
    }

    [Command("unban")]
    [Summary("Unbans a user after confirmation.")]
    public async Task UnbanAsync([Remainder] string? input = null)
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
                "I need `Ban Members` or `Administrator` permission to unban users.");
            return;
        }

        var parsed = ParseInput(input);

        if (parsed is null)
        {
            await ReplyNoticeAsync(
                "Invalid Usage",
                "Usage: `?unban user_id reason` or `?unban @user reason`.");
            return;
        }

        var targetId = ParseUserId(parsed.Value.Target);

        if (targetId is null)
        {
            await ReplyNoticeAsync(
                "Invalid User",
                "Please provide a valid user mention or user ID.");
            return;
        }

        var ban = await GetBanOrNullAsync(targetId.Value);

        if (ban is null)
        {
            await ReplyNoticeAsync(
                "User Not Banned",
                "That user is not banned from this server.");
            return;
        }

        var request = _confirmationService.Create(
            Context.Guild.Id,
            Context.User.Id,
            targetId.Value,
            GetDisplayName(ban.User),
            ban.User.GetDisplayAvatarUrl(size: 256),
            string.IsNullOrWhiteSpace(parsed.Value.Reason)
                ? DefaultReason
                : parsed.Value.Reason);

        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildPrompt(request));
    }

    private async Task<IBan?> GetBanOrNullAsync(ulong userId)
    {
        try
        {
            return await Context.Guild.GetBanAsync(userId);
        }
        catch
        {
            return null;
        }
    }

    private async Task ReplyNoticeAsync(string title, string message)
    {
        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildNotice(title, message));
    }

    private static bool HasBanPermission(GuildPermissions permissions)
    {
        return permissions.BanMembers || permissions.Administrator;
    }

    private static ulong? ParseUserId(string query)
    {
        if (MentionUtils.TryParseUser(query, out var mentionedUserId))
            return mentionedUserId;

        return ulong.TryParse(query, out var userId)
            ? userId
            : null;
    }

    private static ParsedUnbanInput? ParseInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var trimmed = input.Trim();
        var separatorIndex = trimmed.IndexOf(' ');

        if (separatorIndex < 0)
        {
            return new ParsedUnbanInput(trimmed, DefaultReason);
        }

        var target = trimmed[..separatorIndex].Trim();
        var reason = trimmed[(separatorIndex + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(target))
            return null;

        return new ParsedUnbanInput(
            target,
            string.IsNullOrWhiteSpace(reason)
                ? DefaultReason
                : reason);
    }

    private static string GetDisplayName(IUser user)
    {
        return string.IsNullOrWhiteSpace(user.GlobalName)
            ? user.Username
            : user.GlobalName;
    }

    private readonly record struct ParsedUnbanInput(
        string Target,
        string Reason);
}
