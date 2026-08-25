using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;

namespace LastRide.Modules;

[Name("Utility")]
public sealed class UserInfoModule : ModuleBase<SocketCommandContext>
{
    private readonly UserInfoComponentBuilder _builder;

    public UserInfoModule(UserInfoComponentBuilder builder)
    {
        _builder = builder;
    }

    [Command("userinfo")]
    [Alias("user", "ui")]
    [Summary("Shows detailed information about a user.")]
    public async Task UserInfoAsync(SocketUser? user = null)
    {
        var targetUser = user ?? Context.User;
        var guildUser = Context.Guild?.GetUser(targetUser.Id);

        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.Build(targetUser, guildUser));
    }
}
