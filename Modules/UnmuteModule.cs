using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed class UnmuteModule : ModuleBase<SocketCommandContext>
{
    private readonly MuteComponentBuilder _builder;

    public UnmuteModule(MuteComponentBuilder builder)
    {
        _builder = builder;
    }

    [Command("unmute")]
    [Summary("Removes a member's active timeout.")]
    public async Task UnmuteAsync([Remainder] string? input = null)
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
                "You need `Timeout Members` or `Administrator` permission to use this command.");
            return;
        }

        if (!HasModeratePermission(Context.Guild.CurrentUser.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Bot Permission",
                "I need `Timeout Members` or `Administrator` permission to unmute users.");
            return;
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            await ReplyNoticeAsync(
                "Invalid Usage",
                "Usage: `?unmute @user` or `?unmute user_id`.");
            return;
        }

        var target = ResolveTarget(input.Trim());

        if (target is null)
        {
            await ReplyNoticeAsync(
                "User Not Found",
                "I could not find that member. Mention them or provide a valid user ID.");
            return;
        }

        if (target.TimedOutUntil is not { } until ||
            until <= DateTimeOffset.UtcNow)
        {
            await ReplyNoticeAsync(
                "Not Muted",
                $"<@{target.Id}> is not currently muted.");
            return;
        }

        try
        {
            await target.RemoveTimeOutAsync(
                new RequestOptions
                {
                    AuditLogReason = $"Unmuted by {moderator.Username}"
                });

            await ReplyAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildUnmuted(
                    target.Id,
                    target.DisplayName,
                    target.GetDisplayAvatarUrl(size: 256),
                    moderator.Id));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Unmute Error] {exception}");

            await ReplyNoticeAsync(
                "Unmute Failed",
                "I could not unmute this member. Check my permissions and role position.");
        }
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

    private static bool HasModeratePermission(GuildPermissions permissions)
    {
        return permissions.ModerateMembers || permissions.Administrator;
    }
}
