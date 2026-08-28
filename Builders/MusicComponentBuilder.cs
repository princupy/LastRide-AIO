using Discord;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Tracks;

namespace LastRide.Builders;

/// <summary>
/// Renders every music card: the now-playing panel that is posted automatically on
/// each track change, the queue confirmations, the paginated queue listing, and the
/// short notices the control commands reply with.
/// </summary>
public sealed class MusicComponentBuilder
{
    /// <summary>Queue entries shown per page; ten keeps the card under Discord's text limit.</summary>
    public const int QueuePageSize = 10;

    private const string ArrowEmoji = "<:ArrowRight:1541407020257640470>";
    private const int MaxTitleLength = 60;
    private const int ProgressBarLength = 16;

    private static readonly Color AccentColor = ComponentTheme.AccentColor;

    private static readonly IEmote ResumeEmote =
        Emote.Parse("<:icons8play32:1497482498098331648>");

    private static readonly IEmote PauseEmote =
        Emote.Parse("<:icons8pause48:1497482679023571143>");

    private static readonly IEmote SkipEmote =
        Emote.Parse("<:icons8skip64:1497483211679465512>");

    /// <summary>
    /// The panel posted on every track change and by <c>nowplaying</c>. Artwork hangs
    /// on the right exactly like an avatar section elsewhere; a track without artwork
    /// degrades to plain text instead of a broken thumbnail.
    /// </summary>
    public MessageComponent BuildNowPlaying(
        LavalinkTrack track,
        float volume,
        TrackRepeatMode repeatMode,
        bool shuffle,
        int queueLength,
        bool isPaused,
        IUser? requester)
    {
        var body =
            $"> {ArrowEmoji} **Track:** {FormatLink(track)}\n" +
            $"> {ArrowEmoji} **Artist:** `{EscapeInlineCode(Truncate(track.Author, MaxTitleLength))}`\n" +
            $"> {ArrowEmoji} **Duration:** `{FormatDuration(track)}`\n" +
            $"> {ArrowEmoji} **Source:** `{FormatSource(track)}`";

        if (requester is not null)
            body += $"\n> {ArrowEmoji} **Requested by:** {requester.Mention}";

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader("Now Playing", body, track.ArtworkUri?.ToString(), track.Title),
            Divider(),
            new TextDisplayBuilder(
                $"> {ArrowEmoji} **Volume:** `{FormatVolume(volume)}` • " +
                $"**Loop:** `{FormatRepeatMode(repeatMode)}` • " +
                $"**Shuffle:** `{(shuffle ? "On" : "Off")}`\n" +
                $"> {ArrowEmoji} **Up Next:** `{queueLength:N0}` track(s) in the queue"),
            BuildControlRow(isPaused)
        };

        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    /// <summary>
    /// Confirmation for a single track that landed behind something already playing.
    /// </summary>
    public MessageComponent BuildQueued(
        LavalinkTrack track,
        int position,
        int queueLength,
        IUser requester)
    {
        var body =
            $"> {ArrowEmoji} **Track:** {FormatLink(track)}\n" +
            $"> {ArrowEmoji} **Artist:** `{EscapeInlineCode(Truncate(track.Author, MaxTitleLength))}`\n" +
            $"> {ArrowEmoji} **Duration:** `{FormatDuration(track)}`\n" +
            $"> {ArrowEmoji} **Position:** `#{position:N0}` of `{queueLength:N0}`\n" +
            $"> {ArrowEmoji} **Requested by:** {requester.Mention}";

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader("Added to Queue", body, track.ArtworkUri?.ToString(), track.Title)
        };

        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    /// <summary>
    /// Confirmation for a playlist. Individual tracks are not listed — a long playlist
    /// would blow the card limit, and <c>queue</c> already shows them on demand.
    /// </summary>
    public MessageComponent BuildPlaylistQueued(
        string playlistName,
        int addedCount,
        int queueLength,
        IUser requester,
        string? artworkUrl)
    {
        var body =
            $"> {ArrowEmoji} **Playlist:** `{EscapeInlineCode(Truncate(playlistName, MaxTitleLength))}`\n" +
            $"> {ArrowEmoji} **Tracks Added:** `{addedCount:N0}`\n" +
            $"> {ArrowEmoji} **Queue Length:** `{queueLength:N0}`\n" +
            $"> {ArrowEmoji} **Requested by:** {requester.Mention}";

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader("Playlist Queued", body, artworkUrl, playlistName)
        };

        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    /// <summary>
    /// One page of the queue with the current track pinned on top, so the listing
    /// always answers "what is playing and what is next" in a single card.
    /// </summary>
    public MessageComponent BuildQueuePage(
        LavalinkTrack? current,
        TimeSpan? position,
        IReadOnlyList<LavalinkTrack> upcoming,
        int page,
        ulong requesterId,
        TrackRepeatMode repeatMode,
        bool shuffle)
    {
        var totalPages = Math.Max(1, (upcoming.Count + QueuePageSize - 1) / QueuePageSize);
        page = Math.Clamp(page, 0, totalPages - 1);

        var start = page * QueuePageSize;
        var pageTracks = upcoming.Skip(start).Take(QueuePageSize).ToArray();

        var header =
            "## Music Queue\n" +
            (upcoming.Count == 0
                ? "> The queue is empty — nothing lined up after the current track."
                : $"> Showing `{start + 1}`-`{start + pageTracks.Length}` of " +
                  $"`{upcoming.Count:N0}` track(s). Page `{page + 1}`/`{totalPages}`.");

        var components = new List<IMessageComponentBuilder>
        {
            new TextDisplayBuilder(header),
            Divider(),
            new TextDisplayBuilder(BuildCurrentContent(current, position))
        };

        if (pageTracks.Length > 0)
        {
            var lines = pageTracks.Select((track, index) =>
                $"> **#{start + index + 1}** {FormatLink(track)} " +
                $"`{FormatDuration(track)}`");

            components.Add(Divider());
            components.Add(new TextDisplayBuilder(
                "### Up Next\n" + string.Join('\n', lines)));
        }

        var totalDuration = upcoming
            .Where(track => !track.IsLiveStream)
            .Aggregate(TimeSpan.Zero, (total, track) => total + track.Duration);

        components.Add(Divider());
        components.Add(new TextDisplayBuilder(
            $"> {ArrowEmoji} **Total Length:** `{FormatTimeSpan(totalDuration)}` • " +
            $"**Loop:** `{FormatRepeatMode(repeatMode)}` • " +
            $"**Shuffle:** `{(shuffle ? "On" : "Off")}`"));

        components.Add(BuildQueueNavigationRow(page, totalPages, requesterId));

        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    /// <summary>
    /// Posted when a track ends with nothing behind it. The member who queued that track
    /// is mentioned because they are the one who can keep the bot in the channel, and the
    /// countdown is stated so the disconnect a minute later is not a surprise.
    /// </summary>
    public MessageComponent BuildQueueFinished(
        LavalinkTrack track,
        ulong requesterId,
        TimeSpan leaveAfter,
        string prefix)
    {
        var opener = requesterId == 0
            ? "> That was the last track in the queue."
            : $"> <@{requesterId}> that was the last track you queued.";

        var components = new List<IMessageComponentBuilder>
        {
            new TextDisplayBuilder(
                "## Queue Finished\n" +
                opener + "\n" +
                $"> {ArrowEmoji} **Ended:** {FormatLink(track)}\n" +
                $"> {ArrowEmoji} **Next:** nothing — queue up another with " +
                $"`{EscapeInlineCode(prefix)}play <song>`\n" +
                $"> {ArrowEmoji} **Leaving in:** `{FormatLeaveDelay(leaveAfter)}` " +
                "if nothing else is played")
        };

        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    public MessageComponent BuildResult(string title, string message)
    {
        var components = new List<IMessageComponentBuilder>
        {
            new TextDisplayBuilder($"## {EscapeMarkdown(title)}\n> {message}")
        };

        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    public MessageComponent BuildNotice(string title, string message)
    {
        return BuildContainer(
            new TextDisplayBuilder($"## {EscapeMarkdown(title)}\n> {message}"));
    }

    private static string BuildCurrentContent(LavalinkTrack? current, TimeSpan? position)
    {
        if (current is null)
            return "### Now Playing\n> Nothing is playing right now.";

        return
            "### Now Playing\n" +
            $"> {FormatLink(current)}\n" +
            BuildProgressContent(current, position);
    }

    /// <summary>
    /// A text progress bar rather than a repeating edit loop: the card is rendered
    /// once with the position it had when it was requested, so nothing polls Lavalink
    /// or rewrites the message in the background.
    /// </summary>
    private static string BuildProgressContent(LavalinkTrack track, TimeSpan? position)
    {
        if (track.IsLiveStream)
            return $"> {ArrowEmoji} `LIVE` — this is a live stream, there is no position.";

        var elapsed = position ?? TimeSpan.Zero;

        if (elapsed > track.Duration)
            elapsed = track.Duration;

        var filled = track.Duration <= TimeSpan.Zero
            ? 0
            : (int)Math.Round(
                elapsed.TotalSeconds / track.Duration.TotalSeconds * (ProgressBarLength - 1));

        filled = Math.Clamp(filled, 0, ProgressBarLength - 1);

        var bar =
            new string('▬', filled) +
            "🔘" +
            new string('▬', ProgressBarLength - 1 - filled);

        return
            $"> {bar}\n" +
            $"> `{FormatTimeSpan(elapsed)}` / `{FormatTimeSpan(track.Duration)}`";
    }

    /// <summary>
    /// Play, pause and skip under the panel. Whichever of play/pause matches the current
    /// state is greyed out, so the row reads as an indicator as well as a control.
    /// </summary>
    private static ActionRowBuilder BuildControlRow(bool isPaused)
    {
        return new ActionRowBuilder()
            .WithButton(BuildControlButton(
                MusicControl.Resume,
                ResumeEmote,
                isDisabled: !isPaused))
            .WithButton(BuildControlButton(
                MusicControl.Pause,
                PauseEmote,
                isDisabled: isPaused))
            .WithButton(BuildControlButton(
                MusicControl.Skip,
                SkipEmote,
                isDisabled: false));
    }

    private static ButtonBuilder BuildControlButton(
        MusicControl control,
        IEmote emote,
        bool isDisabled)
    {
        return new ButtonBuilder()
            .WithStyle(ButtonStyle.Secondary)
            .WithCustomId(MusicComponentIds.CreateControl(control))
            .WithEmote(emote)
            .WithDisabled(isDisabled);
    }

    private static ActionRowBuilder BuildQueueNavigationRow(
        int page,
        int totalPages,
        ulong requesterId)
    {
        return new ActionRowBuilder()
            .WithButton(
                ButtonBuilder.CreateSecondaryButton(
                    "Previous",
                    MusicComponentIds.CreateQueueNav(page - 1, requesterId))
                    .WithDisabled(page <= 0))
            .WithButton(
                ButtonBuilder.CreateSecondaryButton(
                    "Next",
                    MusicComponentIds.CreateQueueNav(page + 1, requesterId))
                    .WithDisabled(page >= totalPages - 1));
    }

    private static IMessageComponentBuilder BuildHeader(
        string title,
        string content,
        string? artworkUrl,
        string artworkDescription)
    {
        if (string.IsNullOrWhiteSpace(artworkUrl))
            return new TextDisplayBuilder($"## {EscapeMarkdown(title)}\n{content}");

        return new SectionBuilder()
            .WithAccessory(new ThumbnailBuilder(
                new UnfurledMediaItemProperties(artworkUrl),
                description: Truncate(artworkDescription, MaxTitleLength)))
            .AddComponents(
                new TextDisplayBuilder($"## {EscapeMarkdown(title)}"),
                new TextDisplayBuilder(content));
    }

    private static MessageComponent BuildContainer(
        params IMessageComponentBuilder[] components)
    {
        var container = new ContainerBuilder()
            .WithAccentColor(AccentColor)
            .AddComponents(components);

        return new ComponentBuilderV2().AddComponent(container).Build();
    }

    /// <summary>
    /// Markdown link when the track has a URI, plain bold text otherwise. Titles from
    /// user-uploaded sources can contain markdown, so they are escaped either way.
    /// </summary>
    private static string FormatLink(LavalinkTrack track)
    {
        var title = EscapeMarkdown(Truncate(track.Title, MaxTitleLength));

        return track.Uri is null
            ? $"**{title}**"
            : $"[**{title}**]({track.Uri})";
    }

    private static string FormatSource(LavalinkTrack track)
    {
        return string.IsNullOrWhiteSpace(track.SourceName)
            ? "Unknown"
            : EscapeInlineCode(track.SourceName);
    }

    private static string FormatDuration(LavalinkTrack track)
    {
        return track.IsLiveStream ? "LIVE" : FormatTimeSpan(track.Duration);
    }

    private static string FormatTimeSpan(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
            value = TimeSpan.Zero;

        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes}:{value.Seconds:00}";
    }

    /// <summary>
    /// Written as whole minutes when it divides evenly, since the inactivity timeout is
    /// configured in minutes and "2 minutes" reads better than "2:00".
    /// </summary>
    private static string FormatLeaveDelay(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
            value = TimeSpan.Zero;

        if (value.Seconds == 0 && value.TotalMinutes >= 1)
            return $"{(int)value.TotalMinutes} minute(s)";

        return FormatTimeSpan(value);
    }

    private static string FormatVolume(float volume)
    {
        return $"{(int)Math.Round(volume * 100f)}%";
    }

    private static string FormatRepeatMode(TrackRepeatMode repeatMode)
    {
        return repeatMode switch
        {
            TrackRepeatMode.Track => "Track",
            TrackRepeatMode.Queue => "Queue",
            _ => "Off"
        };
    }

    private static void AppendFooter(List<IMessageComponentBuilder> components)
    {
        components.Add(FooterSeparator());
        components.Add(new TextDisplayBuilder(ComponentFooter.Text));
    }

    private static SeparatorBuilder Divider()
    {
        return new SeparatorBuilder(isDivider: true, spacing: SeparatorSpacingSize.Small);
    }

    private static SeparatorBuilder FooterSeparator()
    {
        return new SeparatorBuilder(isDivider: true, spacing: SeparatorSpacingSize.Small);
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Unknown";

        var trimmed = value.Trim();

        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..(maxLength - 1)] + "…";
    }

    private static string EscapeInlineCode(string value)
    {
        return value.Replace("`", "'");
    }

    private static string EscapeMarkdown(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("*", "\\*")
            .Replace("_", "\\_")
            .Replace("~", "\\~")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("`", "'");
    }
}
