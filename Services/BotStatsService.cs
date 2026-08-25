using System.Diagnostics;
using Discord.WebSocket;
using LastRide.Models;

namespace LastRide.Services;

public sealed class BotStatsService
{
    private readonly DiscordSocketClient _client;
    private readonly MongoDbService _mongoDb;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    public BotStatsService(
        DiscordSocketClient client,
        MongoDbService mongoDb)
    {
        _client = client;
        _mongoDb = mongoDb;
    }

    public async Task<BotStatsSnapshot> CaptureAsync()
    {
        var guilds = _client.Guilds;
        var databaseStatus = await _mongoDb.GetStatusAsync();

        var userCount = guilds.Sum(
            static guild => (long)guild.MemberCount);

        var channelCount = guilds.Sum(
            static guild => guild.Channels.Count);

        using var process = Process.GetCurrentProcess();

        var memoryMegabytes =
            process.WorkingSet64 / 1024d / 1024d;

        var discordNetVersion =
            typeof(DiscordSocketClient)
                .Assembly
                .GetName()
                .Version?
                .ToString(3)
            ?? "Unknown";

        return new BotStatsSnapshot(
            GuildCount: guilds.Count,
            UserCount: userCount,
            ChannelCount: channelCount,
            GatewayLatency: _client.Latency,
            Uptime: DateTimeOffset.UtcNow - _startedAt,
            MemoryMegabytes: memoryMegabytes,
            ConnectionState: _client.ConnectionState.ToString(),
            DatabaseStatus: databaseStatus,
            DotNetVersion: Environment.Version.ToString(),
            DiscordNetVersion: discordNetVersion);
    }
}
