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
    private readonly CommandAccessService _accessService;

    public HelpModule(
        HelpComponentBuilder builder,
        PrefixService prefixService,
        CommandAccessService accessService)
    {
        _builder = builder;
        _prefixService = prefixService;
        _accessService = accessService;
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
            Context.Client.CurrentUser.GetDisplayAvatarUrl(size: 256),
            _accessService.TotalCommands,
            _accessService.CountAvailable(Context.User));

        return ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: components);
    }
}
