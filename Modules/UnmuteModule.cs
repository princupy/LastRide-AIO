using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Core;

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
                "Usage: `?unmute @user`. The member has to be mentioned.");
            return;
        }

        if (!UserReference.TryParse(input.Trim(), out var targetId))
        {
            await ReplyNoticeAsync(
                "Mention Required",
                "Mention the member you want to unmute: `?unmute @user`. A user ID works " +
                "too, but a plain name does not — I will not guess who you meant.");
            return;
        }

        var target = Context.Guild.GetUser(targetId);

        if (target is null)
        {
            await ReplyNoticeAsync(
                "User Not Found",
                "That user is not a member of this server.");
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
            Console.WriteLine($"[Unmute Error] {DiscordFailure.Format(exception)}");

            await ReplyNoticeAsync(
                "Unmute Failed",
                DiscordFailure.Describe(
                    exception,
                    "I could not unmute this member. Check my permissions and role position."));
        }
    }

    private async Task ReplyNoticeAsync(string title, string message)
    {
        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildNotice(title, message));
    }

    private static bool HasModeratePermission(GuildPermissions permissions)
    {
        return permissions.ModerateMembers || permissions.Administrator;
    }
}
