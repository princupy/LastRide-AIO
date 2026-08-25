using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Services;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed class SnipeModule : ModuleBase<SocketCommandContext>
{
    private readonly SnipeComponentBuilder _builder;
    private readonly SnipeService _snipeService;

    public SnipeModule(
        SnipeComponentBuilder builder,
        SnipeService snipeService)
    {
        _builder = builder;
        _snipeService = snipeService;
    }

    [Command("snipe")]
    [Alias("s")]
    [Summary("Shows the recently deleted messages in this channel.")]
    public async Task SnipeAsync()
    {
        if (Context.Guild is null)
        {
            await ReplyAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildNotice(
                    "Server Only",
                    "This command can only be used in a server."));
            return;
        }

        var moderator = Context.User as SocketGuildUser;

        if (moderator is null ||
            !HasManageMessages(moderator.GuildPermissions))
        {
            await ReplyAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildNotice(
                    "Missing Permission",
                    "You need `Manage Messages` or `Administrator` permission to use this command."));
            return;
        }

        var messages = _snipeService.GetMessages(Context.Channel.Id);

        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.Build(
                messages,
                moderator.Id,
                Context.Channel.Id,
                0));
    }

    private static bool HasManageMessages(GuildPermissions permissions)
    {
        return permissions.ManageMessages || permissions.Administrator;
    }
}
