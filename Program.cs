using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Configuration;
using LastRide.Core;
using LastRide.Services;
using Microsoft.Extensions.DependencyInjection;

const string defaultPrefix = "?";

LoadDotEnv(".env");

var token = Environment.GetEnvironmentVariable("LASTRIDE_TOKEN");

if (string.IsNullOrWhiteSpace(token))
{
    throw new InvalidOperationException(
        "LASTRIDE_TOKEN environment variable is missing.");
}

var configuredPrefix = Environment.GetEnvironmentVariable("LASTRIDE_PREFIX");
var ownerId = ReadOptionalSnowflake("LASTRIDE_OWNER_ID");
var mongoConnectionString =
    Environment.GetEnvironmentVariable("LASTRIDE_MONGODB_URI") ??
    Environment.GetEnvironmentVariable("MONGODB_URI");

var options = new BotOptions(
    token,
    string.IsNullOrWhiteSpace(configuredPrefix)
        ? defaultPrefix
        : configuredPrefix,
    ownerId,
    string.IsNullOrWhiteSpace(mongoConnectionString)
        ? null
        : mongoConnectionString);

var socketConfig = new DiscordSocketConfig
{
    GatewayIntents =
        GatewayIntents.Guilds |
        GatewayIntents.GuildMembers |
        GatewayIntents.GuildPresences |
        GatewayIntents.GuildMessages |
        GatewayIntents.DirectMessages |
        GatewayIntents.MessageContent,

    AlwaysDownloadUsers = true,
    MessageCacheSize = 50,
    LogGatewayIntentWarnings = false
};

var commandConfig = new CommandServiceConfig
{
    CaseSensitiveCommands = false,
    DefaultRunMode = RunMode.Async,
    LogLevel = LogSeverity.Info
};

await using var services = new ServiceCollection()
    .AddSingleton(options)
    .AddSingleton(new DiscordSocketClient(socketConfig))
    .AddSingleton(new CommandService(commandConfig))
    .AddSingleton<CommandHandler>()
    .AddSingleton<BotRunner>()
    .AddSingleton<BotStatsService>()
    .AddSingleton<BotOwnerService>()
    .AddSingleton<AfkService>()
    .AddSingleton<SnipeService>()
    .AddSingleton<MongoDbService>()
    .AddSingleton<PingComponentBuilder>()
    .AddSingleton<StatsComponentBuilder>()
    .AddSingleton<HelpComponentBuilder>()
    .AddSingleton<ProfileComponentBuilder>()
    .AddSingleton<AfkComponentBuilder>()
    .AddSingleton<MentionComponentBuilder>()
    .AddSingleton<MemberCountComponentBuilder>()
    .AddSingleton<UserInfoComponentBuilder>()
    .AddSingleton<ServerInfoComponentBuilder>()
    .AddSingleton<RoleInfoComponentBuilder>()
    .AddSingleton<SnipeComponentBuilder>()
    .AddSingleton<BanConfirmationService>()
    .AddSingleton<BanComponentBuilder>()
    .AddSingleton<UnbanConfirmationService>()
    .AddSingleton<UnbanComponentBuilder>()
    .AddSingleton<KickConfirmationService>()
    .AddSingleton<KickComponentBuilder>()
    .AddSingleton<MuteComponentBuilder>()
    .AddSingleton<NickComponentBuilder>()
    .AddSingleton<NukeConfirmationService>()
    .AddSingleton<NukeComponentBuilder>()
    .AddSingleton<PurgeComponentBuilder>()
    .AddSingleton<AddRoleComponentBuilder>()
    .AddSingleton<RoleIconService>()
    .AddSingleton<RoleIconComponentBuilder>()
    .AddSingleton<LockComponentBuilder>()
    .AddSingleton<UnlockComponentBuilder>()
    .AddSingleton<LockAllComponentBuilder>()
    .AddSingleton<UnlockAllComponentBuilder>()
    .AddSingleton<HideComponentBuilder>()
    .AddSingleton<UnhideComponentBuilder>()
    .AddSingleton<HideAllComponentBuilder>()
    .AddSingleton<UnhideAllComponentBuilder>()
    .BuildServiceProvider();

await services
    .GetRequiredService<BotRunner>()
    .RunAsync();

static void LoadDotEnv(string path)
{
    if (!File.Exists(path))
        return;

    foreach (var rawLine in File.ReadAllLines(path))
    {
        var line = rawLine.Trim();

        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            continue;

        var separatorIndex = line.IndexOf('=');

        if (separatorIndex <= 0)
            continue;

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim();

        if (value.Length >= 2 &&
            ((value.StartsWith('"') && value.EndsWith('"')) ||
             (value.StartsWith('\'') && value.EndsWith('\''))))
        {
            value = value[1..^1];
        }

        if (string.IsNullOrWhiteSpace(key) ||
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
        {
            continue;
        }

        Environment.SetEnvironmentVariable(key, value);
    }
}

static ulong? ReadOptionalSnowflake(string name)
{
    var value = Environment.GetEnvironmentVariable(name);

    if (string.IsNullOrWhiteSpace(value))
        return null;

    if (ulong.TryParse(value.Trim(), out var id))
        return id;

    throw new InvalidOperationException(
        $"{name} must be a Discord user ID number.");
}
