using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed class UnhideAllModule : ModuleBase<SocketCommandContext>
{
    private readonly UnhideAllComponentBuilder _builder;

    public UnhideAllModule(UnhideAllComponentBuilder builder)
    {
        _builder = builder;
    }

    [Command("unhideall")]
    [Summary("Shows hidden channels and lets you unhide selected channels.")]
    public async Task UnhideAllAsync()
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

        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildMenu(
                Context.Guild,
                moderator.Id));
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
