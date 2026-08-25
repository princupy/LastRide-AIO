namespace LastRide.Models;

public sealed record BotStatsSnapshot(
    int GuildCount,
    long UserCount,
    int ChannelCount,
    int GatewayLatency,
    TimeSpan Uptime,
    double MemoryMegabytes,
    string ConnectionState,
    string DatabaseStatus,
    string DotNetVersion,
    string DiscordNetVersion);
