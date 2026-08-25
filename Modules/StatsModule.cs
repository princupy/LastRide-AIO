using Discord;
using Discord.Commands;
using LastRide.Builders;
using LastRide.Services;

namespace LastRide.Modules;

[Name("General")]
public sealed class StatsModule : ModuleBase<SocketCommandContext>
{
    private readonly BotStatsService _statsService;
    private readonly StatsComponentBuilder _builder;

    public StatsModule(
        BotStatsService statsService,
        StatsComponentBuilder builder)
    {
        _statsService = statsService;
        _builder = builder;
    }

    [Command("stats")]
    [Summary("Shows the bot's runtime statistics.")]
    public async Task StatsAsync()
    {
        var stats = await _statsService.CaptureAsync();
        var components = _builder.BuildGeneral(
            stats,
            Context.User.Id);

        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: components);
    }
}
