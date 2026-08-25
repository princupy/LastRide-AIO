using Discord;

namespace LastRide.Builders;

public sealed class StealComponentBuilder
{
    private static readonly Color AccentColor = new(8, 4, 4);

    public MessageComponent BuildResult(
        IReadOnlyList<string> addedEmojis,
        IReadOnlyList<string> addedStickers,
        int failedCount,
        string? thumbnailUrl,
        ulong moderatorId)
    {
        var lines = new List<string>();

        if (addedEmojis.Count > 0)
        {
            lines.Add($"> **Emojis Added:** `{addedEmojis.Count:N0}`");
            lines.Add("> " + string.Join(" ", addedEmojis));
        }

        if (addedStickers.Count > 0)
        {
            lines.Add($"> **Stickers Added:** `{addedStickers.Count:N0}`");
            lines.Add(
                "> " + string.Join(
                    ", ",
                    addedStickers.Select(name => $"`{EscapeInlineCode(name)}`")));
        }

        if (failedCount > 0)
            lines.Add($"> **Failed:** `{failedCount:N0}`");

        lines.Add($"> **Moderator:** <@{moderatorId}>");

        return BuildContainer(
            BuildHeader(
                "Steal Complete",
                string.Join("\n", lines),
                thumbnailUrl,
                "Stolen"));
    }

    public MessageComponent BuildNotice(
        string title,
        string message)
    {
        return BuildContainer(
            new TextDisplayBuilder(
                $"## {EscapeMarkdown(title)}\n> {message}"));
    }

    private static MessageComponent BuildContainer(
        params IMessageComponentBuilder[] components)
    {
        var container = new ContainerBuilder()
            .WithAccentColor(AccentColor)
            .AddComponents(components);

        return new ComponentBuilderV2()
            .AddComponent(container)
            .Build();
    }

    private static IMessageComponentBuilder BuildHeader(
        string title,
        string content,
        string? thumbnailUrl,
        string thumbnailDescription)
    {
        if (string.IsNullOrWhiteSpace(thumbnailUrl))
        {
            return new TextDisplayBuilder(
                $"## {EscapeMarkdown(title)}\n{content}");
        }

        return new SectionBuilder()
            .WithAccessory(
                new ThumbnailBuilder(
                    new UnfurledMediaItemProperties(thumbnailUrl),
                    description: thumbnailDescription))
            .AddComponents(
                new TextDisplayBuilder($"## {EscapeMarkdown(title)}"),
                new TextDisplayBuilder(content));
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
