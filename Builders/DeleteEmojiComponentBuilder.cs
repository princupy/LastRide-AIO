using Discord;

namespace LastRide.Builders;

public sealed class DeleteEmojiComponentBuilder
{
    private static readonly Color AccentColor = new(8, 4, 4);

    public MessageComponent BuildResult(
        IReadOnlyList<string> deletedEmojis,
        IReadOnlyList<string> deletedStickers,
        int failedCount,
        ulong moderatorId)
    {
        var lines = new List<string>();

        if (deletedEmojis.Count > 0)
        {
            lines.Add($"> **Emojis Deleted:** `{deletedEmojis.Count:N0}`");
            lines.Add(
                "> " + string.Join(
                    ", ",
                    deletedEmojis.Select(name => $"`{EscapeInlineCode(name)}`")));
        }

        if (deletedStickers.Count > 0)
        {
            lines.Add($"> **Stickers Deleted:** `{deletedStickers.Count:N0}`");
            lines.Add(
                "> " + string.Join(
                    ", ",
                    deletedStickers.Select(name => $"`{EscapeInlineCode(name)}`")));
        }

        if (failedCount > 0)
            lines.Add($"> **Failed:** `{failedCount:N0}`");

        lines.Add($"> **Moderator:** <@{moderatorId}>");

        return BuildContainer(
            new TextDisplayBuilder(
                $"## Delete Complete\n{string.Join("\n", lines)}"));
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
