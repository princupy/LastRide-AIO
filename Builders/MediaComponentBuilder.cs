using Discord;
using Discord.WebSocket;
using LastRide.Models;

namespace LastRide.Builders;

/// <summary>
/// Renders the media-only settings card, the short warning posted when a message
/// is removed, and the card a removed message is forwarded with.
/// </summary>
public sealed class MediaComponentBuilder
{
    // The stored set is capped at the same number, so the overflow suffix only
    // ever shows for a document written outside the bot.
    private const int MaxListedChannels = 25;

    // Keeps a forwarded message readable inside the card instead of dominating it.
    private const int MaxForwardedContent = 500;

    private static readonly Color AccentColor = ComponentTheme.AccentColor;

    private const string EnabledEmoji = "<:Enabled:1541831759191212154>";
    private const string DisabledEmoji = "<:disabled:1541831823406014699>";

    public MessageComponent BuildStatus(
        MediaConfig config,
        SocketGuild guild,
        string prefix,
        bool isPersistent)
    {
        var emoji = config.Enabled ? EnabledEmoji : DisabledEmoji;

        // Deleted channels stay in the stored set until someone removes them, so
        // the card only lists the ones that still exist and counts the rest.
        var live = config
            .ChannelIds
            .Where(channelId => guild.GetTextChannel(channelId) is not null)
            .ToArray();

        var lines = new List<string>
        {
            $"> {emoji} **Status:** {(config.Enabled ? "Enabled" : "Disabled")}",
            $"> **Channels:** {FormatChannels(live)}",
            config.ChatChannelId is { } chatChannelId
                ? $"> **Forward Channel:** <#{chatChannelId}>"
                : "> **Forward Channel:** `Not set`"
        };

        var missing = config.ChannelIds.Count - live.Length;

        if (missing > 0)
        {
            lines.Add(
                $"> **Deleted:** `{missing}` saved channel(s) no longer exist.");
        }

        if (config.Enabled && config.ChannelIds.Count == 0)
        {
            lines.Add(
                "> Nothing is enforced yet — a media-only channel still has to be added.");
        }

        if (!config.ForwardsMentions)
        {
            lines.Add(
                "> Mention forwarding is off until a forward channel is set.");
        }

        const string behaviour =
            "### How It Works\n" +
            "> Anything without an image, video, file, sticker or link is removed — " +
            "commands included.\n" +
            "> Nobody is exempt, not even admins or the server owner.\n" +
            "> A removed message that mentioned someone is forwarded to the " +
            "forward channel and pings them.";

        var hint =
            $"-# `{prefix}media setup #channel` • `{prefix}media remove #channel` • " +
            $"`{prefix}media chat set #channel` • `{prefix}media on/off` • " +
            $"`{prefix}media reset`\n" +
            "-# Run these outside a media-only channel — commands are removed there too.";

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader(
                "Media Settings",
                string.Join("\n", lines),
                guild.IconUrl,
                guild.Name),
            Divider(),
            new TextDisplayBuilder(behaviour),
            Divider(),
            new TextDisplayBuilder(hint)
        };

        AppendPersistenceNote(components, isPersistent);
        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    /// <summary>
    /// Posted in the media-only channel itself and deleted again seconds later, so
    /// it stays deliberately short.
    /// </summary>
    public MessageComponent BuildViolationNotice(ulong userId, bool forwarded)
    {
        var content =
            "## Media Only\n" +
            $"> <@{userId}>, this channel accepts media only — send an image, " +
            "video, file, sticker or link.\n" +
            "> **Text is removed here**, commands included.";

        // Tells the member their message was relayed rather than simply lost.
        if (forwarded)
        {
            content +=
                "\n> Your message was forwarded to the server's chat channel.";
        }

        return BuildContainer(new TextDisplayBuilder(content));
    }

    /// <summary>
    /// Relays a removed message to the forward channel. The mentioned members are
    /// listed on their own line rather than relying on the tokens inside the body,
    /// because truncation could cut one off and silently drop the ping.
    /// </summary>
    public MessageComponent BuildForward(
        SocketGuildUser author,
        ulong sourceChannelId,
        IReadOnlyCollection<ulong> mentionIds,
        string? content)
    {
        var body =
            $"> **From:** <@{author.Id}> (`{EscapeInlineCode(author.Username)}`)\n" +
            $"> **To:** {string.Join(" ", mentionIds.Select(id => $"<@{id}>"))}\n" +
            $"> **Channel:** <#{sourceChannelId}>";

        var message = string.IsNullOrWhiteSpace(content)
            ? "> *No text content.*"
            : Quote(EscapeMarkdown(Truncate(content, MaxForwardedContent)));

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader(
                "Message Forwarded",
                body,
                AvatarUrl(author),
                author.DisplayName),
            Divider(),
            new TextDisplayBuilder($"### Message\n{message}")
        };

        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    public MessageComponent BuildResult(string title, string message, bool isPersistent)
    {
        var components = new List<IMessageComponentBuilder>
        {
            new TextDisplayBuilder($"## {EscapeMarkdown(title)}\n> {message}")
        };

        AppendPersistenceNote(components, isPersistent);
        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    public MessageComponent BuildNotice(string title, string message)
    {
        return BuildContainer(
            new TextDisplayBuilder($"## {EscapeMarkdown(title)}\n> {message}"));
    }

    private static MessageComponent BuildContainer(
        params IMessageComponentBuilder[] components)
    {
        var container = new ContainerBuilder()
            .WithAccentColor(AccentColor)
            .AddComponents(components);

        return new ComponentBuilderV2().AddComponent(container).Build();
    }

    private static IMessageComponentBuilder BuildHeader(
        string title,
        string content,
        string? avatarUrl,
        string avatarDescription)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
            return new TextDisplayBuilder($"## {EscapeMarkdown(title)}\n{content}");

        return new SectionBuilder()
            .WithAccessory(new ThumbnailBuilder(
                new UnfurledMediaItemProperties(avatarUrl),
                description: avatarDescription))
            .AddComponents(
                new TextDisplayBuilder($"## {EscapeMarkdown(title)}"),
                new TextDisplayBuilder(content));
    }

    private static void AppendPersistenceNote(
        List<IMessageComponentBuilder> components,
        bool isPersistent)
    {
        if (isPersistent)
            return;

        components.Add(new TextDisplayBuilder(
            "-# Note: settings are active now but will reset when the bot restarts."));
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

    private static string FormatChannels(IReadOnlyCollection<ulong> channelIds)
    {
        if (channelIds.Count == 0)
            return "`None`";

        var listed = channelIds
            .Take(MaxListedChannels)
            .Select(channelId => $"<#{channelId}>")
            .ToList();

        if (channelIds.Count > MaxListedChannels)
            listed.Add($"`+{channelIds.Count - MaxListedChannels} more`");

        return string.Join(" • ", listed);
    }

    private static string Quote(string value)
    {
        // Quotes every line so a multi-line message keeps one continuous bar
        // instead of only marking the first line.
        return string.Join(
            "\n",
            value.Split('\n').Select(line => $"> {line.TrimEnd('\r')}"));
    }

    private static string AvatarUrl(SocketGuildUser member)
    {
        return member.GetDisplayAvatarUrl(size: 256) ?? member.GetDefaultAvatarUrl();
    }

    private static string Truncate(string value, int maxLength)
    {
        // Line breaks are kept rather than collapsed to spaces: the forwarded text
        // is rendered inside a blockquote, not inline code, so its original shape
        // is worth preserving.
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
            .Replace("`", "'");
    }
}
