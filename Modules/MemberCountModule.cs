using Discord;
using Discord.Commands;
using LastRide.Builders;

namespace LastRide.Modules;

[Name("Utility")]
public sealed class MemberCountModule : ModuleBase<SocketCommandContext>
{
    private readonly MemberCountComponentBuilder _builder;

    public MemberCountModule(MemberCountComponentBuilder builder)
    {
        _builder = builder;
    }

    [Command("membercount")]
    [Alias("members", "mc")]
    [Summary("Shows the server member and presence counts.")]
    public async Task MemberCountAsync()
    {
        if (Context.Guild is null)
        {
            await ReplyAsync("This command can only be used in a server.");
            return;
        }

        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.Build(Context.Guild));
    }
}
