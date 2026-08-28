using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Lavalink4NET.Extensions;
using Lavalink4NET.InactivityTracking.Extensions;
using LastRide.Builders;
using LastRide.Configuration;
using LastRide.Core;
using LastRide.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

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
        GatewayIntents.GuildBans |
        GatewayIntents.GuildPresences |
        GatewayIntents.GuildMessages |
        GatewayIntents.GuildVoiceStates |
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
    .AddSingleton<WarnService>()
    .AddSingleton<SnipeService>()
    .AddSingleton<MongoDbService>()
    .AddSingleton<PrefixService>()
    .AddSingleton<AutoModConfigService>()
    .AddSingleton<AutoModService>()
    .AddSingleton<AutoRoleConfigService>()
    .AddSingleton<AutoRoleService>()
    .AddSingleton<AutoResponderConfigService>()
    .AddSingleton<AutoResponderService>()
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
    .AddSingleton<BanListService>()
    .AddSingleton<BanListComponentBuilder>()
    .AddSingleton<KickConfirmationService>()
    .AddSingleton<KickComponentBuilder>()
    .AddSingleton<MuteComponentBuilder>()
    .AddSingleton<WarnComponentBuilder>()
    .AddSingleton<NickComponentBuilder>()
    .AddSingleton<NukeConfirmationService>()
    .AddSingleton<NukeComponentBuilder>()
    .AddSingleton<PurgeComponentBuilder>()
    .AddSingleton<AddRoleComponentBuilder>()
    .AddSingleton<RoleIconService>()
    .AddSingleton<RoleIconComponentBuilder>()
    .AddSingleton<StealService>()
    .AddSingleton<StealComponentBuilder>()
    .AddSingleton<DeleteEmojiComponentBuilder>()
    .AddSingleton<SetPrefixComponentBuilder>()
    .AddSingleton<AutoModComponentBuilder>()
    .AddSingleton<AutoRoleComponentBuilder>()
    .AddSingleton<AutoResponderComponentBuilder>()
    .AddSingleton<LockComponentBuilder>()
    .AddSingleton<UnlockComponentBuilder>()
    .AddSingleton<LockAllComponentBuilder>()
    .AddSingleton<UnlockAllComponentBuilder>()
    .AddSingleton<HideComponentBuilder>()
    .AddSingleton<UnhideComponentBuilder>()
    .AddSingleton<HideAllComponentBuilder>()
    .AddSingleton<UnhideAllComponentBuilder>()
    .AddSingleton<VoiceComponentBuilder>()
    .AddSingleton<LogConfigService>()
    .AddSingleton<LogService>()
    .AddSingleton<LogComponentBuilder>()
    .AddSingleton<LevelConfigService>()
    .AddSingleton<LevelComponentBuilder>()
    .AddSingleton<LevelService>()
    .AddSingleton<SetupRoleConfigService>()
    .AddSingleton<SetupRoleComponentBuilder>()
    .AddSingleton<SetupRoleService>()
    .AddSingleton<WelcomeConfigService>()
    .AddSingleton<WelcomeComponentBuilder>()
    .AddSingleton<WelcomeService>()
    .AddSingleton<TicketConfigService>()
    .AddSingleton<TicketComponentBuilder>()
    .AddSingleton<TicketService>()
    .AddSingleton<MediaConfigService>()
    .AddSingleton<MediaComponentBuilder>()
    .AddSingleton<MediaService>()
    .AddSingleton<GiveawayComponentBuilder>()
    .AddSingleton<GiveawayService>()
    .AddSingleton<NoPrefixComponentBuilder>()
    .AddSingleton<NoPrefixService>()
    .AddSingleton<CommandAccessService>()
    // Lavalink4NET writes through Microsoft.Extensions.Logging, which the bot does
    // not otherwise use. Clearing the providers and adding only the bot's own console
    // writer keeps the audio stack from printing framework-shaped lines, and the
    // Warning floor means a healthy node is completely silent.
    .AddLogging(logging => logging
        .ClearProviders()
        .AddProvider(new LavalinkConsoleLoggerProvider())
        .SetMinimumLevel(LogLevel.Warning)
        .AddFilter("System.Net.Http.HttpClient", LogLevel.None))
    // One node, the TLS endpoint. The operator publishes a plaintext port as well, but
    // it is the same physical server behind a second front door — running both meant a
    // cluster whose round-robin balancer sent every other request to a node that was
    // still connecting, so a single node is both simpler and steadier here.
    .AddLavalink()
    .ConfigureLavalink(options =>
    {
        options.BaseAddress = LavalinkSettings.BaseAddress;
        options.Passphrase = LavalinkSettings.Passphrase;
    })
    // Lavalink4NET's Discord.Net wrapper never observed the bot's own VOICE_STATE_UPDATE,
    // so it sent the node a blank session id and no channel id and every connect came
    // back as a bare 400. This tracker reads that gateway event itself and the handler
    // below completes the outgoing request from it.
    .AddSingleton<VoiceSessionTracker>()
    // Lavalink4NET does not put a channelId on the voice object it sends, and the node
    // rejects an update without one (bare 400), so the bot could connect and search but
    // never start a track. This handler fills the field in on the way out; it is scoped
    // to the audio stack's own HTTP client so no other request is touched.
    .AddTransient<LavalinkVoicePatchHandler>()
    .ConfigureAll<HttpClientFactoryOptions>(http =>
        http.HttpMessageHandlerBuilderActions.Add(builder =>
            builder.AdditionalHandlers.Add(
                builder.Services.GetRequiredService<LavalinkVoicePatchHandler>())))
    // The default trackers cover both "the voice channel is empty" and "nothing is
    // playing"; leaving the behaviour at its default destroys the idle player, which
    // is what makes the bot leave rather than sit in the channel.
    .AddInactivityTracking()
    .ConfigureInactivityTracking(tracking =>
    {
        // Read from the service so the countdown printed on the queue-finished card and
        // the disconnect that follows it can never drift apart.
        tracking.DefaultTimeout = MusicService.InactivityTimeout;
        tracking.DefaultPollInterval = TimeSpan.FromSeconds(15);
    })
    .AddSingleton<MusicComponentBuilder>()
    .AddSingleton<MusicService>()
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
