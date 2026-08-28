using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace LastRide.Core;

/// <summary>
/// Guarantees the <c>sessionId</c> and <c>channelId</c> fields are present on the voice
/// object of every player-update request sent to the node.
/// </summary>
/// <remarks>
/// Lavalink v4's update-player endpoint treats the voice object's four fields —
/// <c>token</c>, <c>endpoint</c>, <c>sessionId</c>, <c>channelId</c> — as the values
/// required to reach a Discord voice server, and its own docs state that
/// <c>channelId</c> is <em>not</em> nullable on an update. Recent node builds enforce
/// that: a voice object with a missing channel id or a blank session id is answered with
/// a bare <c>400 Bad Request</c> and no message.
///
/// Both of those fields come from Discord's <c>VOICE_STATE_UPDATE</c>, and on the
/// Discord.Net wrapper this bot targets Lavalink4NET never observed one — it sent
/// <c>"sessionId": ""</c> and no channel id at all, so every connect was rejected and
/// playback never started. <see cref="VoiceSessionTracker"/> watches that gateway event
/// directly; this handler reads the request the library built and fills in whichever of
/// the two fields is unusable before it goes out.
///
/// Everything else passes straight through untouched, and a request that still fails is
/// printed once in the bot's own console format so a real fault stays visible instead of
/// hiding behind the generic card.
/// </remarks>
public sealed class LavalinkVoicePatchHandler : DelegatingHandler
{
    private readonly VoiceSessionTracker _voiceSessions;

    public LavalinkVoicePatchHandler(VoiceSessionTracker voiceSessions)
    {
        _voiceSessions = voiceSessions;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Only the player-update call carries a voice object, and only it is a PATCH to
        // this path shape — everything else (track loads, info, session) is left alone.
        if (request.Method != HttpMethod.Patch ||
            request.Content is null ||
            !TryGetGuildId(request.RequestUri, out var guildId))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var body = await request.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(body))
        {
            body = await EnsureVoiceStateAsync(body, guildId, cancellationToken)
                .ConfigureAwait(false);

            // Reading the content consumed it, so the request is rebuilt from the final
            // JSON whether or not it was changed — otherwise the send would go out empty.
            request.Content = new StringContent(body, Encoding.UTF8);
            request.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/json");
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss}] [Warning] [Lavalink] " +
                $"Player update {(int)response.StatusCode} for guild {guildId}. " +
                $"Sent: {body} — Node: {detail}");
        }

        return response;
    }

    /// <summary>
    /// Fills in <c>sessionId</c> and <c>channelId</c> when the library left either blank,
    /// using the session <see cref="VoiceSessionTracker"/> captured off the gateway. A body
    /// without a voice object, or a guild the bot has no voice session in, is returned
    /// unchanged.
    /// </summary>
    private async Task<string> EnsureVoiceStateAsync(
        string body,
        ulong guildId,
        CancellationToken cancellationToken)
    {
        JsonNode? root;

        try
        {
            root = JsonNode.Parse(body);
        }
        catch (System.Text.Json.JsonException)
        {
            return body;
        }

        if (root?["voice"] is not JsonObject voice)
            return body;

        var hasSessionId = !string.IsNullOrWhiteSpace(ReadString(voice, "sessionId"));
        var hasChannelId = !string.IsNullOrWhiteSpace(ReadString(voice, "channelId"));

        if (hasSessionId && hasChannelId)
            return body;

        var session = await _voiceSessions
            .ResolveAsync(guildId, cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            // Nothing to patch with. The node will refuse the request, and the response
            // logging below prints the body — so say why here rather than leaving that
            // rejection looking unexplained.
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss}] [Warning] [Lavalink] " +
                $"No voice session for guild {guildId}; Discord never reported one.");

            return body;
        }

        if (!hasSessionId)
            voice["sessionId"] = session.Value.SessionId;

        if (!hasChannelId)
            voice["channelId"] = session.Value.ChannelId.ToString();

        return root!.ToJsonString();
    }

    /// <summary>
    /// Reads a JSON field as a string, treating a wrong type or JSON null as absent.
    /// </summary>
    private static string? ReadString(JsonObject voice, string property)
    {
        if (voice[property] is not JsonValue value)
            return null;

        return value.TryGetValue<string>(out var text) ? text : null;
    }

    /// <summary>
    /// True when the URI is a player route (<c>/v4/sessions/{id}/players/{guildId}</c>),
    /// handing back the trailing guild id.
    /// </summary>
    private static bool TryGetGuildId(Uri? uri, out ulong guildId)
    {
        guildId = 0;

        if (uri is null)
            return false;

        var segments = uri.AbsolutePath.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);

        // Shape: v4 / sessions / {sessionId} / players / {guildId}
        if (segments.Length < 5 ||
            !segments[^2].Equals("players", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ulong.TryParse(segments[^1], out guildId);
    }
}
