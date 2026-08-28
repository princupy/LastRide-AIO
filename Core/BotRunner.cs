using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Configuration;
using LastRide.Services;

namespace LastRide.Core;

public sealed class BotRunner
{
    private static readonly TimeSpan ActivityRotationDelay =
        TimeSpan.FromSeconds(5);

    private readonly DiscordSocketClient _client;
    private readonly CommandService _commands;
    private readonly CommandHandler _commandHandler;
    private readonly MusicService _musicService;
    private readonly BotOptions _options;
    private CancellationTokenSource? _activityRotation;
    private Task? _activityRotationTask;

    public BotRunner(
        DiscordSocketClient client,
        CommandService commands,
        CommandHandler commandHandler,
        MusicService musicService,
        BotOptions options)
    {
        _client = client;
        _commands = commands;
        _commandHandler = commandHandler;
        _musicService = musicService;
        _options = options;
    }

    public async Task RunAsync()
    {
        _client.Log += LogAsync;
        _commands.Log += LogAsync;
        _client.Ready += HandleReadyAsync;

        Console.WriteLine("[Startup] Loading commands...");
        await _commandHandler.InitializeAsync();

        // Lavalink4NET registers its audio service and inactivity tracker as hosted
        // services, and this bot has no generic host to run them — so the start and
        // stop calls happen here instead. Without this, music silently never connects.
        // The socket itself dials once Discord goes ready and then keeps itself alive.
        Console.WriteLine("[Startup] Starting audio service...");
        await _musicService.InitializeAsync();

        Console.WriteLine("[Startup] Logging in...");
        await _client.LoginAsync(TokenType.Bot, _options.Token);

        Console.WriteLine("[Startup] Starting client...");
        await _client.StartAsync();

        Console.WriteLine("[Startup] Client started. Waiting for Discord ready event...");

        var shutdownSignal = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdownSignal.TrySetResult();
        };

        Console.CancelKeyPress += cancelHandler;

        try
        {
            await shutdownSignal.Task;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;

            await StopActivityRotationAsync();
            await _musicService.StopAsync();
            await _client.StopAsync();
            await _client.LogoutAsync();
        }
    }

    private Task HandleReadyAsync()
    {
        StartActivityRotation();

        Console.WriteLine(
            $"[Ready] Logged in as {_client.CurrentUser.Username}");

        return Task.CompletedTask;
    }

    private void StartActivityRotation()
    {
        if (_activityRotationTask is { IsCompleted: false })
            return;

        _activityRotation = new CancellationTokenSource();
        _activityRotationTask = RotateActivityAsync(
            _activityRotation.Token);
    }

    private async Task StopActivityRotationAsync()
    {
        if (_activityRotation is null ||
            _activityRotationTask is null)
        {
            return;
        }

        await _activityRotation.CancelAsync();

        try
        {
            await _activityRotationTask;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _activityRotation.Dispose();
            _activityRotation = null;
            _activityRotationTask = null;
        }
    }

    private async Task RotateActivityAsync(
        CancellationToken cancellationToken)
    {
        var showInvite = true;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (showInvite)
            {
                await _client.SetGameAsync(
                    "discord.gg/thelastride",
                    type: ActivityType.Watching);
            }
            else
            {
                await _client.SetCustomStatusAsync("Following Tanmay..");
            }

            showInvite = !showInvite;

            await Task.Delay(
                ActivityRotationDelay,
                cancellationToken);
        }
    }

    private static Task LogAsync(LogMessage message)
    {
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss}] " +
            $"[{message.Severity}] " +
            $"[{message.Source}] {message.Message}");

        if (message.Exception is not null)
            Console.WriteLine(message.Exception);

        return Task.CompletedTask;
    }
}
