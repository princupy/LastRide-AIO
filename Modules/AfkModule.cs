using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Services;

namespace LastRide.Modules;

[Name("Utility")]
public sealed class AfkModule : ModuleBase<SocketCommandContext>
{
    private readonly AfkService _afkService;
    private readonly AfkComponentBuilder _builder;

    public AfkModule(
        AfkService afkService,
        AfkComponentBuilder builder)
    {
        _afkService = afkService;
        _builder = builder;
    }

    [Command("afk")]
    [Alias("away")]
    [Summary("Marks you as AFK with an optional reason.")]
    public Task AfkAsync([Remainder] string? reason = null)
    {
        var displayName = Context.User is SocketGuildUser guildUser &&
            !string.IsNullOrWhiteSpace(guildUser.DisplayName)
                ? guildUser.DisplayName
                : Context.User.Username;

        var status = _afkService.SetAfk(
            Context.User.Id,
            displayName,
            reason);

        return ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildSet(status));
    }
}
