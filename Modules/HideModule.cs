using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed class HideModule : ModuleBase<SocketCommandContext>
{
    private const string DefaultReason = "No reason provided.";
    private readonly HideComponentBuilder _builder;

    public HideModule(HideComponentBuilder builder)
    {
        _builder = builder;
    }

    [Command("hide")]
    [Summary("Hides the current channel from everyone.")]
    public async Task HideAsync([Remainder] string? reason = null)
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
                "I can only hide standard text channels.");
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

        if (currentOverwrite?.ViewChannel == PermValue.Deny)
        {
            await ReplyNoticeAsync(
                "Already Hidden",
                "This channel is already hidden.");
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
                overwrite.Modify(viewChannel: PermValue.Deny),
                new RequestOptions
                {
                    AuditLogReason =
                        $"Hidden by {moderator.Username}: {reasonText}"
                });

            await ReplyAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildSuccess(
                    channel.Id,
                    moderator.Id));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Hide Error] {exception}");

            await ReplyNoticeAsync(
                "Hide Failed",
                "I could not hide this channel. Check my permissions and role position.");
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
