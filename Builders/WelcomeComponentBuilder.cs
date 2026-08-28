using Discord;
using Discord.WebSocket;
using LastRide.Models;

namespace LastRide.Builders;

/// <summary>
/// Renders the greeting posted when a member joins plus the settings, result,
/// and notice cards the Welcome commands reply with.
/// </summary>
public sealed class WelcomeComponentBuilder
{
    private static readonly Color AccentColor = ComponentTheme.AccentColor;

    private const string EnabledEmoji = "<:Enabled:1541831759191212154>";
    private const string DisabledEmoji = "<:disabled:1541831823406014699>";

    public MessageComponent BuildWelcome(
        SocketGuildUser member,
        string message,
        bool isTest)
    {
        // No heading and no member-count line by design: the card shows only the
        // configured greeting, so anything extra has to come from the template's
        // own placeholders.
        var greeting = new SectionBuilder()
            .WithAccessory(new ThumbnailBuilder(
                new UnfurledMediaItemProperties(AvatarUrl(member)),
                description: member.DisplayName))
            .AddComponents(new TextDisplayBuilder(Quote(message)));

        var components = new List<IMessageComponentBuilder> { greeting };

        // Marks the preview so a test card is never mistaken for a real join.
        if (isTest)
        {
            components.Add(new TextDisplayBuilder(
                "-# Preview only — nobody joined the server."));
        }

        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    public MessageComponent BuildStatus(
        WelcomeConfig config,
        SocketGuild guild,
        string prefix,
        bool isPersistent,
        string? note)
    {
        var emoji = config.Enabled ? EnabledEmoji : DisabledEmoji;

        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(note))
            lines.Add($"> {note}");

        lines.Add($"> {emoji} **Status:** {(config.Enabled ? "Enabled" : "Disabled")}");
        lines.Add(config.ChannelId is { } channelId
            ? $"> **Channel:** <#{channelId}>"
            : "> **Channel:** `Not set`");
        lines.Add($"> **Message:** {FormatTemplate(config.Message)}");

        if (config.Enabled && config.ChannelId is null)
        {
            lines.Add(
                "> Nothing is posted yet — a welcome channel still has to be set.");
        }

        const string placeholders =
            "### Placeholders\n" +
            "> `{user}` — mentions the member • `{username}` — their display name\n" +
            "> `{server}` — this server's name • `{membercount}` — total members";

        var hint =
            $"-# `{prefix}welcomechannel set #channel` • `{prefix}welcomemessage <text>` • " +
            $"`{prefix}welcome on/off` • `{prefix}welcome test` • `{prefix}welcome reset`";

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader(
                "Welcome Settings",
                string.Join("\n", lines),
                guild.IconUrl,
                guild.Name),
            Divider(),
            new TextDisplayBuilder(placeholders),
            Divider(),
            new TextDisplayBuilder(hint)
        };

        AppendPersistenceNote(components, isPersistent);
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

    private static string Quote(string value)
    {
        // Quotes every line so a multi-line greeting keeps one continuous bar
        // instead of only marking the first line.
        return string.Join(
            "\n",
            value.Split('\n').Select(line => $"> {line.TrimEnd('\r')}"));
    }

    private static string FormatTemplate(string? template)
    {
        return string.IsNullOrWhiteSpace(template)
            ? "`Default`"
            : $"`{EscapeInlineCode(Truncate(template, 120))}`";
    }

    private static string AvatarUrl(SocketGuildUser member)
    {
        return member.GetDisplayAvatarUrl(size: 256) ?? member.GetDefaultAvatarUrl();
    }

    private static string Truncate(string value, int maxLength)
    {
        var collapsed = value.Replace("\n", " ").Replace("\r", " ").Trim();

        return collapsed.Length <= maxLength
            ? collapsed
            : collapsed[..(maxLength - 1)] + "…";
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
