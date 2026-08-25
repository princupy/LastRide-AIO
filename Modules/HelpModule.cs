using Discord;
using Discord.Commands;
using LastRide.Builders;
using LastRide.Configuration;

namespace LastRide.Modules;

[Name("General")]
public sealed class HelpModule : ModuleBase<SocketCommandContext>
{
    private readonly HelpComponentBuilder _builder;
    private readonly BotOptions _options;

    public HelpModule(
        HelpComponentBuilder builder,
        BotOptions options)
    {
        _builder = builder;
        _options = options;
    }

    [Command("help")]
    [Alias("commands", "h")]
    [Summary("Shows LastRide's command menu.")]
    public Task HelpAsync()
    {
        var components = _builder.Build(
            Context.User.Id,
            _options.Prefix,
            Context.User.Mention,
            Context.Client.CurrentUser.Username,
            Context.Client.CurrentUser.GetDisplayAvatarUrl(size: 256));

        return ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: components);
    }
}
