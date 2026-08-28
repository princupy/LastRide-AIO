using Discord.WebSocket;
using System.Collections.Concurrent;

namespace LastRide.Core;

/// <summary>
/// The bot's own voice session per guild, as Discord reported it.
/// </summary>
/// <param name="ChannelId">Voice channel the bot is connected to.</param>
/// <param name="SessionId">
/// Voice session id from <c>VOICE_STATE_UPDATE</c>. Lavalink needs this exact value to
/// authenticate the voice websocket on the bot's behalf.
/// </param>
public readonly record struct VoiceSessionSnapshot(
    ulong ChannelId,
    string SessionId);

/// <summary>
/// Records the bot's voice session for every guild straight off the gateway.
/// </summary>
/// <remarks>
/// Lavalink's update-player endpoint needs four values to reach a Discord voice
/// server: <c>token</c> and <c>endpoint</c> (from <c>VOICE_SERVER_UPDATE</c>), plus
/// <c>sessionId</c> and <c>channelId</c> (from <c>VOICE_STATE_UPDATE</c>). In this bot
/// only the first pair ever arrived — Lavalink4NET's Discord.Net wrapper was sending an
/// empty session id and no channel id at all, and the node answered every connect with
/// a bare <c>400</c>, so playback never started.
///
/// Rather than work around one field at a time, this tracker subscribes to the gateway
/// event itself and keeps the authoritative pair. <see cref="LavalinkVoicePatchHandler"/>
/// reads it on the way out, so the request that leaves the process is always complete
/// regardless of what the library managed to observe.
/// </remarks>
public sealed class VoiceSessionTracker
{
    /// <summary>
    /// How long a caller waits for the gateway to report the session. Discord sends the
    /// state and server updates back to back, so this is only ever a race guard — it
    /// exists because Lavalink builds its request off the server update and may win.
    /// </summary>
    private static readonly TimeSpan ResolveTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    private readonly DiscordSocketClient _client;

    private readonly ConcurrentDictionary<ulong, VoiceSessionSnapshot> _sessions = new();

    private int _isInitialized;

    public VoiceSessionTracker(DiscordSocketClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Subscribes to the gateway. Called before login so no voice event is missed, and
    /// guarded so a second call cannot double-subscribe.
    /// </summary>
    public void Initialize()
    {
        if (Interlocked.Exchange(ref _isInitialized, 1) != 0)
            return;

        _client.UserVoiceStateUpdated += HandleVoiceStateUpdatedAsync;
    }

    /// <summary>
    /// Hands back the bot's voice session for a guild, waiting a short while when the
    /// gateway has not caught up yet. Null means the bot is not in a voice channel there
    /// — or Discord never reported a session id, which is worth knowing about.
    /// </summary>
    public async ValueTask<VoiceSessionSnapshot?> ResolveAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + ResolveTimeout;

        while (true)
        {
            if (TryRead(guildId, out var snapshot))
                return snapshot;

            if (DateTimeOffset.UtcNow >= deadline ||
                cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            try
            {
                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Non-blocking read. Prefers the value captured off the event, and falls back to the
    /// guild's cached voice state so a session recorded before this tracker existed — a
    /// reconnect, a resumed gateway session — is still usable.
    /// </summary>
    public bool TryRead(ulong guildId, out VoiceSessionSnapshot snapshot)
    {
        if (_sessions.TryGetValue(guildId, out snapshot))
            return true;

        var state = _client.GetGuild(guildId)?.CurrentUser?.VoiceState;
        var channelId = state?.VoiceChannel?.Id;
        var sessionId = state?.VoiceSessionId;

        if (channelId is null || string.IsNullOrWhiteSpace(sessionId))
        {
            snapshot = default;

            return false;
        }

        snapshot = new VoiceSessionSnapshot(channelId.Value, sessionId);
        _sessions[guildId] = snapshot;

        return true;
    }

    /// <summary>
    /// Drops the remembered session, so a stale one is never sent after the bot has been
    /// moved or disconnected.
    /// </summary>
    public void Forget(ulong guildId)
    {
        _sessions.TryRemove(guildId, out _);
    }

    private Task HandleVoiceStateUpdatedAsync(
        SocketUser user,
        SocketVoiceState before,
        SocketVoiceState after)
    {
        // Only the bot's own session is a credential Lavalink can use; every other
        // member's voice state is irrelevant here.
        if (user.Id != _client.CurrentUser?.Id)
            return Task.CompletedTask;

        // The channel carries the guild on a join, and the previous channel does on a
        // leave. Neither is set when the channel is not cached, so the member itself is
        // the first choice.
        var guild = (user as SocketGuildUser)?.Guild
            ?? (after.VoiceChannel ?? before.VoiceChannel)?.Guild;

        if (guild is null)
            return Task.CompletedTask;

        if (after.VoiceChannel is null)
        {
            _sessions.TryRemove(guild.Id, out _);

            return Task.CompletedTask;
        }

        // A state update without a session id would overwrite a good value with an
        // unusable one, so it is ignored rather than stored.
        if (string.IsNullOrWhiteSpace(after.VoiceSessionId))
            return Task.CompletedTask;

        _sessions[guild.Id] = new VoiceSessionSnapshot(
            after.VoiceChannel.Id,
            after.VoiceSessionId);

        return Task.CompletedTask;
    }
}
