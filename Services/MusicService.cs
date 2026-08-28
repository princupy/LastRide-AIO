using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Lavalink4NET;
using Lavalink4NET.Clients;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Events;
using Lavalink4NET.Events.Players;
using Lavalink4NET.InactivityTracking;
using Lavalink4NET.InactivityTracking.Events;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using LastRide.Builders;
using LastRide.Core;
using System.Collections.Concurrent;

namespace LastRide.Services;

/// <summary>Why a player could not be handed back to a command.</summary>
public enum MusicPlayerResult
{
    Success,

    /// <summary>The member is not in any voice channel.</summary>
    NotInVoice,

    /// <summary>The member is in a different voice channel than the bot.</summary>
    DifferentVoice,

    /// <summary>Nothing is playing — the bot is not in a voice channel at all.</summary>
    NotConnected,

    /// <summary>No Lavalink node is reachable right now.</summary>
    Unavailable
}

public readonly record struct MusicPlayerOutcome(
    MusicPlayerResult Result,
    QueuedLavalinkPlayer? Player);

/// <summary>How a track lookup ended.</summary>
public enum MusicSearchResult
{
    Success,

    /// <summary>The query is fine, the sources simply had nothing for it.</summary>
    NoMatches,

    /// <summary>The audio server could not be reached or did not answer in time.</summary>
    Unavailable
}

public readonly record struct MusicSearchOutcome(
    MusicSearchResult Result,
    TrackLoadResult Tracks);

/// <summary>
/// Where a player card was posted. The channel travels with the message id so a card can
/// still be removed after later commands moved playback to a different channel.
/// </summary>
internal readonly record struct PlayerCardReference(
    ulong ChannelId,
    ulong MessageId);

/// <summary>
/// A queued track plus the member who asked for it. The library's own queue item carries
/// only the track, and the bot needs the requester to know who to talk to when the queue
/// runs dry.
/// </summary>
public sealed record MusicQueueItem(TrackReference Reference, ulong RequesterId)
    : ITrackQueueItem
{
    public MusicQueueItem(LavalinkTrack track, ulong requesterId)
        : this(new TrackReference(track), requesterId)
    {
    }
}

/// <summary>
/// Owns everything audio: node lifecycle, player retrieval, track lookup, and the
/// cards that are posted without a command behind them (track changes, playback
/// faults, inactivity disconnects).
/// </summary>
/// <remarks>
/// The bot builds a bare <c>ServiceProvider</c> and drives its own lifecycle in
/// <see cref="Core.BotRunner"/>, so the <c>IHostedService</c> adapters that
/// Lavalink4NET registers for the audio service and the inactivity tracker never
/// run. <see cref="InitializeAsync"/> and <see cref="StopAsync"/> stand in for them
/// and must be called from <see cref="Core.BotRunner"/>.
/// </remarks>
public sealed class MusicService
{
    /// <summary>
    /// A stuck or failing stream can fault on every track in a row. One notice per
    /// window keeps a bad source from turning into a wall of cards.
    /// </summary>
    private static readonly TimeSpan ErrorNoticeWindow = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Ceiling for a single node request, so an unreachable server answers with a card
    /// instead of leaving the command waiting forever.
    /// </summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// How long the queue-finished check waits after a track ends. The player starts the
    /// next queued track immediately after the event, so reading the queue straight away
    /// would race the very transition being tested for.
    /// </summary>
    private static readonly TimeSpan QueueDrainGrace = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Mirrors the inactivity timeout configured in <c>Program</c>. Only used to state the
    /// countdown on the queue-finished card; the disconnect itself is the tracker's job.
    /// </summary>
    public static readonly TimeSpan InactivityTimeout = TimeSpan.FromMinutes(2);

    private readonly IAudioService _audioService;
    private readonly IInactivityTrackingService _inactivityTracking;
    private readonly DiscordSocketClient _client;
    private readonly MusicComponentBuilder _builder;
    private readonly VoiceSessionTracker _voiceSessions;
    private readonly PrefixService _prefixService;

    /// <summary>
    /// Guild to the channel the last music command ran in. Automatic cards have no
    /// command context of their own, so this is where they go.
    /// </summary>
    private readonly ConcurrentDictionary<ulong, ulong> _textChannels = new();

    private readonly ConcurrentDictionary<ulong, DateTimeOffset> _lastErrorNotice = new();

    /// <summary>
    /// Guild to the player card currently on screen. Only one is kept alive: when a new
    /// track starts, the panel for the previous one is deleted rather than left behind as
    /// a row of dead buttons. The channel is stored alongside the message because the card
    /// it replaces may have been posted in a different channel.
    /// </summary>
    private readonly ConcurrentDictionary<ulong, PlayerCardReference> _playerCards = new();

    /// <summary>
    /// Guild to the member who queued the track currently playing. Recorded on every track
    /// start so the queue-finished card knows who to mention once the queue runs dry.
    /// </summary>
    private readonly ConcurrentDictionary<ulong, ulong> _currentRequesters = new();

    private bool _isStopping;

    public MusicService(
        IAudioService audioService,
        IInactivityTrackingService inactivityTracking,
        DiscordSocketClient client,
        MusicComponentBuilder builder,
        VoiceSessionTracker voiceSessions,
        PrefixService prefixService)
    {
        _audioService = audioService;
        _inactivityTracking = inactivityTracking;
        _client = client;
        _builder = builder;
        _voiceSessions = voiceSessions;
        _prefixService = prefixService;
    }

    /// <summary>
    /// Subscribes the automatic surfaces and starts the audio service. The node dials
    /// once the Discord client is ready, and from then on Lavalink4NET's own socket
    /// keeps it alive — it re-dials forever with a backoff strategy and resets its
    /// counters on success, so nothing here retries.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Subscribed before login so the bot's very first voice session is captured.
        _voiceSessions.Initialize();

        _audioService.ConnectionReady += HandleConnectionReadyAsync;
        _audioService.ConnectionClosed += HandleConnectionClosedAsync;
        _audioService.TrackStarted += HandleTrackStartedAsync;
        _audioService.TrackEnded += HandleTrackEndedAsync;
        _audioService.TrackException += HandleTrackExceptionAsync;
        _audioService.TrackStuck += HandleTrackStuckAsync;
        _inactivityTracking.PlayerInactive += HandlePlayerInactiveAsync;

        await _audioService.StartAsync();
        await _inactivityTracking.StartAsync();
    }

    public async Task StopAsync()
    {
        _isStopping = true;

        try
        {
            await _inactivityTracking.StopAsync();
            await _audioService.StopAsync();
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Lavalink Shutdown Error] {exception.Message}");
        }
    }

    /// <summary>
    /// Remembers where automatic cards for this guild should go. Called by every
    /// music command so the channel follows the conversation.
    /// </summary>
    public void RegisterTextChannel(SocketCommandContext context)
    {
        if (context.Guild is null)
            return;

        _textChannels[context.Guild.Id] = context.Channel.Id;
    }

    /// <summary>
    /// Retrieves the guild's player, optionally connecting it to the caller's channel.
    /// The concrete <see cref="QueuedLavalinkPlayer"/> is returned rather than the
    /// interface because volume, repeat mode, shuffle and the queue all live on the
    /// class — <c>ILavalinkPlayer</c> only exposes a read-only volume.
    /// </summary>
    /// <remarks>
    /// The voice checks are done here rather than left to the library's retrieve options:
    /// every music command requires the caller to be sitting in the bot's channel, so
    /// someone listening from elsewhere — or from no channel at all — cannot queue tracks
    /// or drive playback for the people who are actually in there.
    /// </remarks>
    public async ValueTask<MusicPlayerOutcome> GetPlayerAsync(
        SocketCommandContext context,
        bool allowConnect,
        CancellationToken cancellationToken = default)
    {
        var callerChannelId = (context.User as IVoiceState)?.VoiceChannel?.Id
            ?? context.Guild.GetUser(context.User.Id)?.VoiceChannel?.Id;

        if (callerChannelId is null)
            return new MusicPlayerOutcome(MusicPlayerResult.NotInVoice, null);

        var retrieveOptions = new PlayerRetrieveOptions(
            ChannelBehavior: allowConnect
                ? PlayerChannelBehavior.Join
                : PlayerChannelBehavior.None);

        try
        {
            using var timeout = CreateTimeout(cancellationToken);

            // Checked before the retrieve so a mismatch never has the side effect of
            // moving the bot, which PlayerChannelBehavior.Join would otherwise do.
            var existing = await GetExistingPlayerAsync(context.Guild.Id, timeout.Token)
                .ConfigureAwait(false);

            if (existing is not null && existing.VoiceChannelId != callerChannelId)
                return new MusicPlayerOutcome(MusicPlayerResult.DifferentVoice, null);

            if (existing is null && !allowConnect)
                return new MusicPlayerOutcome(MusicPlayerResult.NotConnected, null);

            var result = await _audioService.Players
                .RetrieveAsync(
                    context,
                    PlayerFactory.Queued,
                    retrieveOptions,
                    timeout.Token)
                .ConfigureAwait(false);

            if (result.IsSuccess)
                return new MusicPlayerOutcome(MusicPlayerResult.Success, result.Player);

            var translated = Translate(result.Status);

            // The three states a member can fix themselves get their own card. Anything
            // else falls back to the generic one, so the real status is printed once to
            // keep it findable instead of hidden behind "unavailable".
            if (translated is MusicPlayerResult.Unavailable)
            {
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] [Warning] [Lavalink] " +
                    $"Player request refused: {result.Status}.");
            }

            return new MusicPlayerOutcome(translated, null);
        }
        catch (Exception exception) when (IsNodeFailure(exception))
        {
            Report("player", exception);

            return new MusicPlayerOutcome(MusicPlayerResult.Unavailable, null);
        }
    }

    /// <summary>
    /// Fetches a guild's player without a command behind it, for the queue page
    /// buttons. Returns null when the bot has since left the channel, so a stale card
    /// refreshes into an honest "not connected" answer instead of a crash.
    /// </summary>
    public ValueTask<QueuedLavalinkPlayer?> GetExistingPlayerAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        return _audioService.Players
            .GetPlayerAsync<QueuedLavalinkPlayer>(guildId, cancellationToken);
    }

    /// <summary>
    /// Looks a query up, preferring YouTube and falling back to SoundCloud without
    /// telling the caller. Public nodes do not all ship the YouTube source plugin, and
    /// when it is missing the search simply returns nothing instead of failing loudly.
    /// </summary>
    public async ValueTask<MusicSearchOutcome> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        query = query.Trim();

        try
        {
            using var timeout = CreateTimeout(cancellationToken);

            // A pasted link must not get a search prefix, or Lavalink would look the
            // URL up as plain text instead of resolving it.
            var searchMode = Uri.IsWellFormedUriString(query, UriKind.Absolute)
                ? TrackSearchMode.None
                : TrackSearchMode.YouTube;

            var result = await _audioService.Tracks
                .LoadTracksAsync(query, searchMode, cancellationToken: timeout.Token)
                .ConfigureAwait(false);

            if (result.IsSuccess && result.HasMatches)
                return new MusicSearchOutcome(MusicSearchResult.Success, result);

            // A link that did not resolve will not resolve as a SoundCloud search
            // either, so only plain queries get the second attempt.
            if (searchMode == TrackSearchMode.None)
                return new MusicSearchOutcome(MusicSearchResult.NoMatches, result);

            result = await _audioService.Tracks
                .LoadTracksAsync(
                    query,
                    TrackSearchMode.SoundCloud,
                    cancellationToken: timeout.Token)
                .ConfigureAwait(false);

            return result.IsSuccess && result.HasMatches
                ? new MusicSearchOutcome(MusicSearchResult.Success, result)
                : new MusicSearchOutcome(MusicSearchResult.NoMatches, result);
        }
        catch (Exception exception) when (IsNodeFailure(exception))
        {
            Report("search", exception);

            return new MusicSearchOutcome(MusicSearchResult.Unavailable, default);
        }
    }

    /// <summary>
    /// Prints why a request was turned into an "unavailable" card. The card itself is
    /// deliberately vague, so without this line a real fault — a rejected passphrase, a
    /// missing source plugin — would look identical to the node simply being offline.
    /// </summary>
    private static void Report(string stage, Exception exception)
    {
        var detail = exception.InnerException ?? exception;

        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss}] [Warning] [Lavalink] {stage} request failed: " +
            $"{detail.GetType().Name} — {detail.Message}");
    }

    /// <summary>
    /// Flattens the queue into plain tracks for the listing card. Entries whose track
    /// failed to resolve are dropped rather than rendered as a blank row.
    /// </summary>
    public static IReadOnlyList<LavalinkTrack> Snapshot(QueuedLavalinkPlayer player)
    {
        return player.Queue
            .Select(item => item.Track)
            .Where(track => track is not null)
            .Select(track => track!)
            .ToArray();
    }

    private static MusicPlayerResult Translate(PlayerRetrieveStatus status)
    {
        return status switch
        {
            PlayerRetrieveStatus.UserNotInVoiceChannel => MusicPlayerResult.NotInVoice,
            PlayerRetrieveStatus.VoiceChannelMismatch => MusicPlayerResult.DifferentVoice,
            PlayerRetrieveStatus.BotNotConnected => MusicPlayerResult.NotConnected,
            _ => MusicPlayerResult.Unavailable
        };
    }

    /// <summary>
    /// Wraps the caller's token with a hard ceiling. Without one an unreachable node
    /// leaves the request hanging until the HTTP stack gives up, which reads to the
    /// member as the command having been swallowed.
    /// </summary>
    private static CancellationTokenSource CreateTimeout(CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(RequestTimeout);

        return source;
    }

    /// <summary>
    /// True when the failure is the audio server being unreachable rather than a bug.
    /// The usual shape of an outage is a cancellation: the request sits waiting on a
    /// socket that never answers until <see cref="RequestTimeout"/> gives up. The
    /// <see cref="InvalidOperationException"/> arm covers the node reporting that it
    /// has no session to work with.
    /// </summary>
    private static bool IsNodeFailure(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => true,
            TimeoutException => true,
            HttpRequestException => true,
            InvalidOperationException => true,
            AggregateException aggregate =>
                aggregate.InnerExceptions.All(IsNodeFailure),
            _ => exception.InnerException is { } inner && IsNodeFailure(inner)
        };
    }

    private Task HandleConnectionReadyAsync(object sender, ConnectionReadyEventArgs eventArgs)
    {
        // Fires on the first handshake and on every later reconnect, so this doubles
        // as the "node is back" line without any polling.
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss}] [Info] [Lavalink] Node connected and ready.");

        return Task.CompletedTask;
    }

    private Task HandleConnectionClosedAsync(object sender, ConnectionClosedEventArgs eventArgs)
    {
        // Shutdown closes every socket on purpose; printing that would just be noise
        // after the bot has already said it is stopping.
        if (_isStopping)
            return Task.CompletedTask;

        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss}] [Warning] [Lavalink] " +
            "Node connection closed — reconnecting in the background.");

        return Task.CompletedTask;
    }

    private async Task HandleTrackStartedAsync(object sender, TrackStartedEventArgs eventArgs)
    {
        if (eventArgs.Player is not QueuedLavalinkPlayer player)
            return;

        var track = eventArgs.Track;

        // Recorded even when the card cannot be posted: the queue-finished mention later
        // depends on this and not on anything the panel did.
        if (player.CurrentItem is MusicQueueItem item)
            _currentRequesters[player.GuildId] = item.RequesterId;
        else
            _currentRequesters.TryRemove(player.GuildId, out _);

        var channel = ResolveTextChannel(player.GuildId);

        if (channel is null)
            return;

        await ReplacePlayerCardAsync(
            player.GuildId,
            channel,
            _builder.BuildNowPlaying(
                track,
                player.Volume,
                player.RepeatMode,
                player.Shuffle,
                player.Queue.Count,
                player.State is PlayerState.Paused,
                null));
    }

    /// <summary>
    /// Closes out a queue that has run dry: the panel comes down and the member who queued
    /// the last track is mentioned, because they are the one who can keep the bot in the
    /// channel before the inactivity tracker disconnects it.
    /// </summary>
    /// <remarks>
    /// Only a natural finish is handled. A skip, a stop or a replaced track already gets an
    /// answer from the command or button that caused it, so reacting here as well would
    /// post the same news twice.
    /// </remarks>
    private async Task HandleTrackEndedAsync(object sender, TrackEndedEventArgs eventArgs)
    {
        if (eventArgs.Reason is not TrackEndReason.Finished)
            return;

        if (eventArgs.Player is not QueuedLavalinkPlayer player)
            return;

        var guildId = player.GuildId;

        // Doubles as the duplicate guard: the requester is written once per track start, so
        // a repeated event for the same track finds nothing left to take.
        if (!_currentRequesters.TryRemove(guildId, out var requesterId))
            return;

        await Task.Delay(QueueDrainGrace).ConfigureAwait(false);

        // Something followed after all — either the next queued track, a repeat, or a fresh
        // request that landed during the grace window.
        if (player.CurrentTrack is not null || player.State is PlayerState.Destroyed)
            return;

        await DeletePlayerCardAsync(guildId);

        var channel = ResolveTextChannel(guildId);

        if (channel is null)
            return;

        await SendAsync(
            channel,
            _builder.BuildQueueFinished(
                eventArgs.Track,
                requesterId,
                InactivityTimeout,
                _prefixService.GetPrefix(guildId)),
            // The one music card that pings, and only ever the single member who queued
            // the track that just ended — no roles, no everyone, nobody else.
            new AllowedMentions
            {
                AllowedTypes = AllowedMentionTypes.None,
                UserIds = new List<ulong> { requesterId }
            });
    }

    private Task HandleTrackExceptionAsync(object sender, TrackExceptionEventArgs eventArgs)
    {
        return NotifyPlaybackProblemAsync(
            eventArgs.Player,
            "Playback Error",
            "That track failed to play, so I moved on to the next one.");
    }

    private Task HandleTrackStuckAsync(object sender, TrackStuckEventArgs eventArgs)
    {
        return NotifyPlaybackProblemAsync(
            eventArgs.Player,
            "Track Stuck",
            "That track stopped responding, so I moved on to the next one.");
    }

    private async Task NotifyPlaybackProblemAsync(
        ILavalinkPlayer player,
        string title,
        string message)
    {
        var channel = ResolveTextChannel(player.GuildId);

        if (channel is null)
            return;

        var now = DateTimeOffset.UtcNow;
        var last = _lastErrorNotice.GetValueOrDefault(player.GuildId);

        if (now - last < ErrorNoticeWindow)
            return;

        _lastErrorNotice[player.GuildId] = now;

        await SendAsync(channel, _builder.BuildNotice(title, message));
    }

    private async Task HandlePlayerInactiveAsync(object sender, PlayerInactiveEventArgs eventArgs)
    {
        // The bot is leaving, so the panel on screen can no longer control anything.
        await DeletePlayerCardAsync(eventArgs.Player.GuildId);

        var channel = ResolveTextChannel(eventArgs.Player.GuildId);

        if (channel is null)
            return;

        await SendAsync(
            channel,
            _builder.BuildNotice(
                "Left Voice Channel",
                "Nothing was playing for a while, so I disconnected to free the channel."));
    }

    private IMessageChannel? ResolveTextChannel(ulong guildId)
    {
        if (!_textChannels.TryGetValue(guildId, out var channelId))
            return null;

        return _client.GetGuild(guildId)?.GetTextChannel(channelId);
    }

    /// <summary>
    /// Automatic cards go out with mentions disabled by default — nothing here is a reply
    /// to a member, so nothing here should ping one. The queue-finished card is the single
    /// exception and passes its own scoped allowance.
    /// </summary>
    private static Task<IUserMessage?> SendAsync(
        IMessageChannel channel,
        MessageComponent components)
    {
        return SendAsync(channel, components, AllowedMentions.None);
    }

    private static async Task<IUserMessage?> SendAsync(
        IMessageChannel channel,
        MessageComponent components,
        AllowedMentions allowedMentions)
    {
        try
        {
            return await channel.SendMessageAsync(
                allowedMentions: allowedMentions,
                components: components);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Music Card Error] {exception.Message}");

            return null;
        }
    }

    /// <summary>
    /// Posts a player card and removes the one it replaces, so a channel never collects a
    /// stack of panels whose buttons steer a track that finished long ago.
    /// </summary>
    /// <remarks>
    /// The new card goes up before the old one comes down. A failed delete then leaves a
    /// duplicate, which is recoverable, instead of a moment with no controls at all.
    /// </remarks>
    public async Task ReplacePlayerCardAsync(
        ulong guildId,
        IMessageChannel channel,
        MessageComponent components)
    {
        var posted = await SendAsync(channel, components);

        if (posted is null)
            return;

        if (_playerCards.TryGetValue(guildId, out var previous) &&
            previous.MessageId != posted.Id)
        {
            await DeleteQuietlyAsync(previous);
        }

        _playerCards[guildId] = new PlayerCardReference(channel.Id, posted.Id);
    }

    /// <summary>
    /// Drops the tracked player card. Used when playback ends without another track
    /// following, where no replacement panel is coming to take its place.
    /// </summary>
    public async Task DeletePlayerCardAsync(ulong guildId)
    {
        if (!_playerCards.TryRemove(guildId, out var card))
            return;

        await DeleteQuietlyAsync(card);
    }

    /// <summary>
    /// Deleting a card is housekeeping, so a card someone already removed by hand — or a
    /// channel the bot lost access to — is not worth failing a command over.
    /// </summary>
    private async Task DeleteQuietlyAsync(PlayerCardReference card)
    {
        try
        {
            if (await _client.GetChannelAsync(card.ChannelId) is not IMessageChannel channel)
                return;

            var message = await channel.GetMessageAsync(card.MessageId);

            if (message is not null)
                await message.DeleteAsync();
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Music Card Error] {exception.Message}");
        }
    }
}
