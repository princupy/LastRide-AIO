using Discord;
using Discord.Commands;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Tracks;
using LastRide.Builders;
using LastRide.Services;

namespace LastRide.Modules;

[Name("Music")]
public sealed class MusicModule : ModuleBase<SocketCommandContext>
{
    /// <summary>
    /// Lavalink accepts far more, but anything past this starts clipping instead of
    /// getting louder, so the ceiling is set where the audio still holds together.
    /// </summary>
    private const int MaxVolume = 200;

    /// <summary>
    /// A single link can resolve to a thousand-track playlist. Taking the first slice
    /// keeps one command from filling the queue for everyone else in the server.
    /// </summary>
    private const int MaxPlaylistTracks = 100;

    private readonly MusicService _service;
    private readonly MusicComponentBuilder _builder;
    private readonly PrefixService _prefixService;

    public MusicModule(
        MusicService service,
        MusicComponentBuilder builder,
        PrefixService prefixService)
    {
        _service = service;
        _builder = builder;
        _prefixService = prefixService;
    }

    [Command("play")]
    [Alias("p")]
    [Summary("Plays a song or adds it to the queue.")]
    public async Task PlayAsync([Remainder] string? query = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}play <song name or link>`");

            return;
        }

        // Only play and join are allowed to pull the bot into a channel; every other
        // command refuses instead, so nobody can move the bot mid-song by accident.
        var player = await ResolvePlayerAsync(allowConnect: true);

        if (player is null)
            return;

        var outcome = await _service.SearchAsync(query);

        if (outcome.Result is MusicSearchResult.Unavailable)
        {
            await ReplyNoticeAsync(
                "Music Unavailable",
                "The audio server is not responding right now. It reconnects on its " +
                "own, so try again in a moment.");

            return;
        }

        var result = outcome.Tracks;

        if (outcome.Result is MusicSearchResult.NoMatches)
        {
            await ReplyNoticeAsync(
                "Nothing Found",
                $"I could not find anything for {Inline(query)}. Try a different " +
                "name, or paste a direct link.");

            return;
        }

        if (result.IsPlaylist)
        {
            await EnqueuePlaylistAsync(player, result.Tracks, result.Playlist?.Name);
            return;
        }

        var track = result.Track;

        if (track is null)
        {
            await ReplyNoticeAsync(
                "Nothing Found",
                $"I could not find anything for {Inline(query)}.");

            return;
        }

        // Zero means the track went straight to the speakers; anything else is the
        // slot it landed in behind whatever is already playing. The caller is attached to
        // the item so the queue-finished card knows who to mention later.
        var position = await player.PlayAsync(new MusicQueueItem(track, Context.User.Id));

        if (position == 0)
        {
            // The automatic Now Playing card covers this case, so replying here as
            // well would post the same panel twice.
            return;
        }

        await ReplyComponentsAsync(
            _builder.BuildQueued(track, position, player.Queue.Count, Context.User));
    }

    [Command("pause")]
    [Summary("Pauses the current track.")]
    public async Task PauseAsync()
    {
        var player = await ResolvePlayerAsync(allowConnect: false);

        if (player is null)
            return;

        if (player.CurrentTrack is null)
        {
            await ReplyNoticeAsync("Nothing Playing", "There is no track to pause.");
            return;
        }

        if (player.State is PlayerState.Paused)
        {
            await ReplyNoticeAsync(
                "Already Paused",
                $"Use `{Prefix}resume` to start it again.");

            return;
        }

        await player.PauseAsync();

        await ReplyResultAsync(
            "Paused",
            $"Playback is on hold. Use `{Prefix}resume` to continue.");
    }

    [Command("resume")]
    [Alias("unpause")]
    [Summary("Resumes a paused track.")]
    public async Task ResumeAsync()
    {
        var player = await ResolvePlayerAsync(allowConnect: false);

        if (player is null)
            return;

        if (player.CurrentTrack is null)
        {
            await ReplyNoticeAsync("Nothing Playing", "There is no track to resume.");
            return;
        }

        if (player.State is not PlayerState.Paused)
        {
            await ReplyNoticeAsync("Already Playing", "Playback is not paused.");
            return;
        }

        await player.ResumeAsync();
        await ReplyResultAsync("Resumed", "Playback continues.");
    }

    [Command("skip")]
    [Alias("s")]
    [Summary("Skips to the next track in the queue.")]
    public async Task SkipAsync()
    {
        var player = await ResolvePlayerAsync(allowConnect: false);

        if (player is null)
            return;

        var current = player.CurrentTrack;

        if (current is null)
        {
            await ReplyNoticeAsync("Nothing Playing", "There is no track to skip.");
            return;
        }

        await player.SkipAsync();

        // With an empty queue the skip stops playback outright, and no track-start
        // card will follow — so the reply has to say which of the two happened.
        var message = player.CurrentTrack is null
            ? $"Skipped {Inline(current.Title)}. The queue is now empty."
            : $"Skipped {Inline(current.Title)}.";

        await ReplyResultAsync("Skipped", message);
    }

    [Command("stop")]
    [Summary("Stops playback and clears the queue.")]
    public async Task StopAsync()
    {
        var player = await ResolvePlayerAsync(allowConnect: false);

        if (player is null)
            return;

        if (player.CurrentTrack is null && player.Queue.Count == 0)
        {
            await ReplyNoticeAsync("Nothing Playing", "There is nothing to stop.");
            return;
        }

        await player.StopAsync();

        // Nothing follows a stop, so no replacement panel is coming — the one on screen
        // would sit there with live buttons and no track behind them.
        await _service.DeletePlayerCardAsync(Context.Guild.Id);

        await ReplyResultAsync(
            "Stopped",
            $"Playback stopped and the queue is cleared. Use `{Prefix}play` to start " +
            "something new.");
    }

    [Command("queue")]
    [Alias("q")]
    [Summary("Shows the queue with page buttons.")]
    public async Task QueueAsync(string? page = null)
    {
        var player = await ResolvePlayerAsync(allowConnect: false);

        if (player is null)
            return;

        var requestedPage = int.TryParse(page, out var parsed) && parsed > 0
            ? parsed - 1
            : 0;

        await ReplyComponentsAsync(_builder.BuildQueuePage(
            player.CurrentTrack,
            player.Position?.Position,
            MusicService.Snapshot(player),
            requestedPage,
            Context.User.Id,
            player.RepeatMode,
            player.Shuffle));
    }

    [Command("nowplaying")]
    [Alias("np", "current")]
    [Summary("Shows what is playing right now.")]
    public async Task NowPlayingAsync()
    {
        var player = await ResolvePlayerAsync(allowConnect: false);

        if (player is null)
            return;

        var track = player.CurrentTrack;

        if (track is null)
        {
            await ReplyNoticeAsync(
                "Nothing Playing",
                $"Use `{Prefix}play <song>` to start something.");

            return;
        }

        // Routed through the service so this panel becomes the tracked one: the next
        // track change replaces it instead of leaving two sets of live buttons behind.
        await _service.ReplacePlayerCardAsync(
            Context.Guild.Id,
            Context.Channel,
            _builder.BuildNowPlaying(
                track,
                player.Volume,
                player.RepeatMode,
                player.Shuffle,
                player.Queue.Count,
                player.State is PlayerState.Paused,
                null));
    }

    [Command("volume")]
    [Alias("vol")]
    [Summary("Shows or sets playback volume.")]
    public async Task VolumeAsync(string? value = null)
    {
        var player = await ResolvePlayerAsync(allowConnect: false);

        if (player is null)
            return;

        if (string.IsNullOrWhiteSpace(value))
        {
            await ReplyResultAsync(
                "Volume",
                $"Currently at `{(int)Math.Round(player.Volume * 100f)}%`. " +
                $"Use `{Prefix}volume <0-{MaxVolume}>` to change it.");

            return;
        }

        if (!int.TryParse(value.Trim().TrimEnd('%'), out var volume) ||
            volume < 0 ||
            volume > MaxVolume)
        {
            await ReplyNoticeAsync(
                "Invalid Volume",
                $"Give me a number between `0` and `{MaxVolume}`.");

            return;
        }

        await player.SetVolumeAsync(volume / 100f);
        await ReplyResultAsync("Volume Set", $"Volume is now `{volume}%`.");
    }

    [Command("seek")]
    [Summary("Jumps to a position in the current track.")]
    public async Task SeekAsync([Remainder] string? input = null)
    {
        var player = await ResolvePlayerAsync(allowConnect: false);

        if (player is null)
            return;

        var track = player.CurrentTrack;

        if (track is null)
        {
            await ReplyNoticeAsync("Nothing Playing", "There is no track to seek in.");
            return;
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}seek 1:30`, `{Prefix}seek 90`, `{Prefix}seek 1m30s`, " +
                $"`{Prefix}seek +30`, or `{Prefix}seek -10`");

            return;
        }

        if (!track.IsSeekable)
        {
            await ReplyNoticeAsync(
                "Not Seekable",
                "This track is a live stream, so there is no position to jump to.");

            return;
        }

        if (!TryParseSeek(input, out var offset, out var isRelative))
        {
            await ReplyNoticeAsync(
                "Invalid Position",
                $"{Inline(input)} is not a position — use `1:30`, `90`, `1m30s`, " +
                "`+30`, or `-10`.");

            return;
        }

        var target = isRelative
            ? (player.Position?.Position ?? TimeSpan.Zero) + offset
            : offset;

        // Clamping instead of refusing: asking for -30 near the start or +60 near the
        // end is a normal thing to do, and the edge is the obvious answer.
        if (target < TimeSpan.Zero)
            target = TimeSpan.Zero;

        if (target > track.Duration)
            target = track.Duration;

        await player.SeekAsync(target);

        await ReplyResultAsync(
            "Seeked",
            $"Jumped to `{FormatTimeSpan(target)}` of `{FormatTimeSpan(track.Duration)}`.");
    }

    [Command("loop")]
    [Alias("repeat")]
    [Summary("Sets loop mode to off, track, or queue.")]
    public async Task LoopAsync(string? mode = null)
    {
        var player = await ResolvePlayerAsync(allowConnect: false);

        if (player is null)
            return;

        TrackRepeatMode target;

        if (string.IsNullOrWhiteSpace(mode))
        {
            // No argument cycles off to track to queue and back, so the command works
            // as a plain toggle without anyone remembering the mode names.
            target = player.RepeatMode switch
            {
                TrackRepeatMode.None => TrackRepeatMode.Track,
                TrackRepeatMode.Track => TrackRepeatMode.Queue,
                _ => TrackRepeatMode.None
            };
        }
        else
        {
            switch (mode.Trim().ToLowerInvariant())
            {
                case "off":
                case "none":
                case "no":
                    target = TrackRepeatMode.None;
                    break;

                case "track":
                case "song":
                case "one":
                case "current":
                    target = TrackRepeatMode.Track;
                    break;

                case "queue":
                case "all":
                    target = TrackRepeatMode.Queue;
                    break;

                default:
                    await ReplyNoticeAsync(
                        "Invalid Mode",
                        $"Use `{Prefix}loop off`, `{Prefix}loop track`, or " +
                        $"`{Prefix}loop queue`.");

                    return;
            }
        }

        player.RepeatMode = target;

        var message = target switch
        {
            TrackRepeatMode.Track => "The current track will repeat.",
            TrackRepeatMode.Queue => "The whole queue will repeat.",
            _ => "Looping is off."
        };

        await ReplyResultAsync("Loop Mode", message);
    }

    [Command("shuffle")]
    [Summary("Toggles shuffled playback.")]
    public async Task ShuffleAsync()
    {
        var player = await ResolvePlayerAsync(allowConnect: false);

        if (player is null)
            return;

        var enabled = !player.Shuffle;
        player.Shuffle = enabled;

        if (enabled)
        {
            // The flag alone only affects what gets picked next. Shuffling the queue
            // as well makes the change visible in the listing straight away.
            await player.Queue.ShuffleAsync();
        }

        await ReplyResultAsync(
            "Shuffle",
            enabled
                ? "Shuffle is on and the queue has been mixed."
                : "Shuffle is off — the queue plays in order again.");
    }

    [Command("remove")]
    [Alias("rm")]
    [Summary("Removes one track from the queue by its number.")]
    public async Task RemoveAsync(string? position = null)
    {
        var player = await ResolvePlayerAsync(allowConnect: false);

        if (player is null)
            return;

        if (player.Queue.Count == 0)
        {
            await ReplyNoticeAsync("Empty Queue", "There is nothing in the queue.");
            return;
        }

        if (!int.TryParse(position, out var index) ||
            index < 1 ||
            index > player.Queue.Count)
        {
            await ReplyNoticeAsync(
                "Invalid Number",
                $"Pick a number between `1` and `{player.Queue.Count}` — " +
                $"`{Prefix}queue` shows them.");

            return;
        }

        // Read the title before the removal, because after it the index points at a
        // different track.
        var title = player.Queue[index - 1].Track?.Title ?? "Unknown";

        await player.Queue.RemoveAtAsync(index - 1);

        await ReplyResultAsync(
            "Removed",
            $"Removed {Inline(title)} from the queue. `{player.Queue.Count:N0}` " +
            "track(s) left.");
    }

    /// <summary>
    /// Shares its name with <c>purge</c>'s <c>clear</c> alias. The higher priority wins
    /// the bare invocation, while <c>clear 50</c> fails this overload's argument check
    /// and falls through to purge exactly as before.
    /// </summary>
    [Command("clear")]
    [Alias("clearqueue", "cq")]
    [Summary("Clears every queued track.")]
    [Priority(1)]
    public async Task ClearAsync()
    {
        var player = await ResolvePlayerAsync(allowConnect: false);

        if (player is null)
            return;

        var removed = player.Queue.Count;

        if (removed == 0)
        {
            await ReplyNoticeAsync("Empty Queue", "There is nothing to clear.");
            return;
        }

        await player.Queue.ClearAsync();

        await ReplyResultAsync(
            "Queue Cleared",
            $"Removed `{removed:N0}` track(s). The current track keeps playing.");
    }

    [Command("join")]
    [Alias("summon", "connect")]
    [Summary("Joins your voice channel.")]
    public async Task JoinAsync()
    {
        var player = await ResolvePlayerAsync(allowConnect: true);

        if (player is null)
            return;

        var channel = Context.Guild.GetVoiceChannel(player.VoiceChannelId);

        await ReplyResultAsync(
            "Connected",
            channel is null
                ? "I am in your voice channel."
                : $"Joined {Inline(channel.Name)}.");
    }

    [Command("leave")]
    [Alias("dc", "disconnect")]
    [Summary("Leaves the voice channel and clears the queue.")]
    public async Task LeaveAsync()
    {
        var player = await ResolvePlayerAsync(allowConnect: false);

        if (player is null)
            return;

        await player.DisconnectAsync();

        // Disposing drops the player from the manager, so the next play starts from a
        // clean queue instead of resuming an abandoned one.
        await player.DisposeAsync();

        await _service.DeletePlayerCardAsync(Context.Guild.Id);

        await ReplyResultAsync(
            "Disconnected",
            $"Left the voice channel. Use `{Prefix}play` to start again.");
    }

    /// <summary>
    /// Adds a playlist and reports it as one card rather than one per track.
    /// </summary>
    private async Task EnqueuePlaylistAsync(
        QueuedLavalinkPlayer player,
        IReadOnlyList<LavalinkTrack> tracks,
        string? playlistName)
    {
        var accepted = Math.Min(tracks.Count, MaxPlaylistTracks);
        var added = 0;
        string? artworkUrl = null;

        for (var index = 0; index < accepted; index++)
        {
            var track = tracks[index];
            artworkUrl ??= track.ArtworkUri?.ToString();

            await player.PlayAsync(new MusicQueueItem(track, Context.User.Id));
            added++;
        }

        if (added == 0)
        {
            await ReplyNoticeAsync(
                "Empty Playlist",
                "That playlist has no playable tracks.");

            return;
        }

        await ReplyComponentsAsync(_builder.BuildPlaylistQueued(
            string.IsNullOrWhiteSpace(playlistName) ? "Playlist" : playlistName,
            added,
            player.Queue.Count,
            Context.User,
            artworkUrl));
    }

    /// <summary>
    /// Every command starts here: server check, remember the channel for automatic
    /// cards, then hand back the player or explain why there isn't one.
    /// </summary>
    private async Task<QueuedLavalinkPlayer?> ResolvePlayerAsync(bool allowConnect)
    {
        if (!await EnsureGuildAsync())
            return null;

        _service.RegisterTextChannel(Context);

        var outcome = await _service.GetPlayerAsync(Context, allowConnect);

        if (outcome.Result is MusicPlayerResult.Success && outcome.Player is not null)
            return outcome.Player;

        switch (outcome.Result)
        {
            case MusicPlayerResult.NotInVoice:
                await ReplyNoticeAsync(
                    "Join a Voice Channel",
                    "Get into a voice channel first, then run the command again.");
                break;

            case MusicPlayerResult.DifferentVoice:
                await ReplyNoticeAsync(
                    "Wrong Voice Channel",
                    "You have to be in the same voice channel as me to control " +
                    "playback.");
                break;

            case MusicPlayerResult.NotConnected:
                await ReplyNoticeAsync(
                    "Not Connected",
                    $"I am not in a voice channel. Use `{Prefix}play <song>` to " +
                    "start something.");
                break;

            default:
                await ReplyNoticeAsync(
                    "Music Unavailable",
                    "The audio server is not reachable right now. I keep retrying in " +
                    "the background — try again in a moment.");
                break;
        }

        return null;
    }

    /// <summary>
    /// Accepts <c>1:30</c>, <c>1:02:03</c>, <c>90</c>, <c>1m30s</c>, and the same forms
    /// behind a <c>+</c> or <c>-</c> sign for a jump relative to the current position.
    /// </summary>
    private static bool TryParseSeek(string input, out TimeSpan offset, out bool isRelative)
    {
        offset = TimeSpan.Zero;
        isRelative = false;

        var token = input.Trim();

        if (token.Length == 0)
            return false;

        var sign = 1;

        if (token[0] is '+' or '-')
        {
            isRelative = true;
            sign = token[0] == '-' ? -1 : 1;
            token = token[1..].Trim();
        }

        if (token.Length == 0)
            return false;

        if (!TryParseClock(token, out var parsed) && !TryParseUnits(token, out parsed))
            return false;

        offset = sign < 0 ? -parsed : parsed;
        return true;
    }

    /// <summary>Parses <c>mm:ss</c> and <c>h:mm:ss</c>.</summary>
    private static bool TryParseClock(string token, out TimeSpan value)
    {
        value = TimeSpan.Zero;

        if (!token.Contains(':'))
            return false;

        var parts = token.Split(':', StringSplitOptions.TrimEntries);

        if (parts.Length is < 2 or > 3)
            return false;

        var numbers = new int[parts.Length];

        for (var index = 0; index < parts.Length; index++)
        {
            if (!int.TryParse(parts[index], out numbers[index]) || numbers[index] < 0)
                return false;
        }

        value = parts.Length == 2
            ? new TimeSpan(0, numbers[0], numbers[1])
            : new TimeSpan(numbers[0], numbers[1], numbers[2]);

        return true;
    }

    /// <summary>
    /// Parses bare seconds and compound unit forms such as <c>1h2m3s</c>. Written the
    /// same way as the giveaway duration parser — one pass, no regex.
    /// </summary>
    private static bool TryParseUnits(string token, out TimeSpan value)
    {
        value = TimeSpan.Zero;

        if (int.TryParse(token, out var seconds) && seconds >= 0)
        {
            value = TimeSpan.FromSeconds(seconds);
            return true;
        }

        var total = TimeSpan.Zero;
        var digits = 0;
        var hasDigits = false;
        var hasUnit = false;

        foreach (var character in token)
        {
            if (char.IsAsciiDigit(character))
            {
                digits = digits * 10 + (character - '0');
                hasDigits = true;
                continue;
            }

            if (!hasDigits)
                return false;

            var unit = char.ToLowerInvariant(character);

            total += unit switch
            {
                'h' => TimeSpan.FromHours(digits),
                'm' => TimeSpan.FromMinutes(digits),
                's' => TimeSpan.FromSeconds(digits),
                _ => TimeSpan.MinValue
            };

            if (total == TimeSpan.MinValue)
                return false;

            digits = 0;
            hasDigits = false;
            hasUnit = true;
        }

        // A trailing number without a unit, like "1m30", is a typo rather than a
        // position — refusing it is clearer than guessing which unit was meant.
        if (!hasUnit || hasDigits)
            return false;

        value = total;
        return true;
    }

    private static string FormatTimeSpan(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
            value = TimeSpan.Zero;

        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes}:{value.Seconds:00}";
    }

    private async Task<bool> EnsureGuildAsync()
    {
        if (Context.Guild is not null)
            return true;

        await ReplyNoticeAsync("Server Only", "This command can only be used in a server.");
        return false;
    }

    private string Prefix => _prefixService.GetPrefix(Context.Guild?.Id);

    private Task ReplyComponentsAsync(MessageComponent components)
    {
        return ReplyAsync(allowedMentions: AllowedMentions.None, components: components);
    }

    private Task ReplyResultAsync(string title, string message)
    {
        return ReplyComponentsAsync(_builder.BuildResult(title, message));
    }

    private Task ReplyNoticeAsync(string title, string message)
    {
        return ReplyComponentsAsync(_builder.BuildNotice(title, message));
    }

    private static string Inline(string value)
    {
        return $"`{value.Replace("`", "'")}`";
    }
}
