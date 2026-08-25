using System.Diagnostics;
using Discord;
using Discord.Commands;
using LastRide.Builders;
using LastRide.Services;

namespace LastRide.Modules;

[Name("General")]
public sealed class PingModule : ModuleBase<SocketCommandContext>
{
    private readonly PingComponentBuilder _builder;
    private readonly MongoDbService _mongoDb;

    public PingModule(
        PingComponentBuilder builder,
        MongoDbService mongoDb)
    {
        _builder = builder;
        _mongoDb = mongoDb;
    }

    [Command("ping")]
    [Alias("latency")]
    [Summary("Shows LastRide's current latency.")]
    public async Task PingAsync()
    {
        var apiTimer = Stopwatch.StartNew();
        await Context.Client.Rest.GetCurrentUserAsync();
        apiTimer.Stop();

        var databaseLatency = await _mongoDb.GetLatencyAsync();

        var avatarUrl =
            Context.Client.CurrentUser.GetDisplayAvatarUrl(size: 256);

        var components = _builder.Build(
            apiLatency: apiTimer.ElapsedMilliseconds,
            databaseLatency: databaseLatency,
            botAvatarUrl: avatarUrl);

        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: components);
    }
}
