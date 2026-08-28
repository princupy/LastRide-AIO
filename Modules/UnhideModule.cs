using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Core;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed class UnhideModule : ModuleBase<SocketCommandContext>
{
    private const string DefaultReason = "No reason provided.";
    private readonly UnhideComponentBuilder _builder;

    public UnhideModule(UnhideComponentBuilder builder)
    {
        _builder = builder;
    }

    [Command("unhide")]
    [Summary("Unhides the current channel for everyone.")]
    public async Task UnhideAsync([Remainder] string? reason = null)
    {
        if (Context.Guild is null)
        {
            await ReplyNoticeAsync(
                "Server Only",
                "This command can only be used in a server.");
            return;
        }

        if (Context.Channel is not SocketTextChannel channel ||
            Context.Channel is SocketThreadChannel)
        {
            await ReplyNoticeAsync(
                "Unsupported Channel",
                "I can only unhide standard text channels.");
            return;
        }

        var moderator = Context.User as SocketGuildUser;

        if (moderator is null ||
            !HasManageChannels(moderator.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "You need `Manage Channels` or `Administrator` permission to use this command.");
            return;
        }

        if (!CanEditPermissions(Context.Guild.CurrentUser.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Bot Permission",
                "I need `Manage Roles` or `Administrator` permission to edit channel permissions.");
            return;
        }

        var everyoneRole = Context.Guild.EveryoneRole;
        var currentOverwrite = channel.GetPermissionOverwrite(everyoneRole);

        if (currentOverwrite?.ViewChannel != PermValue.Deny)
        {
            await ReplyNoticeAsync(
                "Not Hidden",
                "This channel is not currently hidden.");
            return;
        }

        var reasonText = string.IsNullOrWhiteSpace(reason)
            ? DefaultReason
            : reason.Trim();

        try
        {
            var overwrite = currentOverwrite ?? OverwritePermissions.InheritAll;

            await channel.AddPermissionOverwriteAsync(
                everyoneRole,
                overwrite.Modify(viewChannel: PermValue.Inherit),
                new RequestOptions
                {
                    AuditLogReason =
                        $"Unhidden by {moderator.Username}: {reasonText}"
                });

            await ReplyAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildSuccess(
                    channel.Id,
                    moderator.Id));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Unhide Error] {DiscordFailure.Format(exception)}");

            await ReplyNoticeAsync(
                "Unhide Failed",
                DiscordFailure.Describe(
                    exception,
                    "I could not unhide this channel. Check my permissions and role position."));
        }
    }

    private async Task ReplyNoticeAsync(string title, string message)
    {
        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildNotice(title, message));
    }

    private static bool HasManageChannels(GuildPermissions permissions)
    {
        return permissions.ManageChannels || permissions.Administrator;
    }

    private static bool CanEditPermissions(GuildPermissions permissions)
    {
        return permissions.ManageRoles || permissions.Administrator;
    }
}
