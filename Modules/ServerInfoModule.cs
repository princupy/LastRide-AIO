using Discord;
using Discord.Commands;
using LastRide.Builders;

namespace LastRide.Modules;

[Name("Utility")]
public sealed class ServerInfoModule : ModuleBase<SocketCommandContext>
{
    private readonly ServerInfoComponentBuilder _builder;

    public ServerInfoModule(ServerInfoComponentBuilder builder)
    {
        _builder = builder;
    }

    [Command("serverinfo")]
    [Alias("server", "si")]
    [Summary("Shows detailed server information across multiple pages.")]
    public async Task ServerInfoAsync()
    {
        if (Context.Guild is null)
        {
            await ReplyAsync("This command can only be used in a server.");
            return;
        }

        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.Build(
                Context.Guild,
                Context.User.Id,
                ServerInfoPage.Overview));
    }
}
