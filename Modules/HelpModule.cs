using Discord;
using Discord.Commands;
using LastRide.Builders;
using LastRide.Services;

namespace LastRide.Modules;

[Name("General")]
public sealed class HelpModule : ModuleBase<SocketCommandContext>
{
    private readonly HelpComponentBuilder _builder;
    private readonly PrefixService _prefixService;

    public HelpModule(
        HelpComponentBuilder builder,
        PrefixService prefixService)
    {
        _builder = builder;
        _prefixService = prefixService;
    }

    [Command("help")]
    [Alias("commands", "h")]
    [Summary("Shows LastRide's command menu.")]
    public Task HelpAsync()
    {
        var components = _builder.Build(
            Context.User.Id,
            _prefixService.GetPrefix(Context.Guild?.Id),
            Context.User.Mention,
            Context.Client.CurrentUser.Username,
            Context.Client.CurrentUser.GetDisplayAvatarUrl(size: 256));

        return ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: components);
    }
}
