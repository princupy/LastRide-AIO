using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Services;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed class NukeModule : ModuleBase<SocketCommandContext>
{
    private readonly NukeComponentBuilder _builder;
    private readonly NukeConfirmationService _confirmationService;

    public NukeModule(
        NukeComponentBuilder builder,
        NukeConfirmationService confirmationService)
    {
        _builder = builder;
        _confirmationService = confirmationService;
    }

    [Command("nuke")]
    [Summary("Deletes this channel and recreates an identical copy after confirmation.")]
    public async Task NukeAsync()
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
                "I can only nuke standard text channels.");
            return;
        }

        var moderator = Context.User as SocketGuildUser;

        if (moderator is null ||
            !moderator.GuildPermissions.Administrator)
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "You need `Administrator` permission to use this command.");
            return;
        }

        if (!Context.Guild.CurrentUser.GuildPermissions.ManageChannels &&
            !Context.Guild.CurrentUser.GuildPermissions.Administrator)
        {
            await ReplyNoticeAsync(
                "Missing Bot Permission",
                "I need `Manage Channels` or `Administrator` permission to nuke channels.");
            return;
        }

        var request = _confirmationService.Create(
            Context.Guild.Id,
            Context.User.Id,
            channel.Id,
            channel.Name);

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
}
