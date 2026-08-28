using Discord;

namespace LastRide.Builders;

public sealed class PurgeComponentBuilder
{
    private static readonly Color AccentColor = ComponentTheme.AccentColor;

    public MessageComponent BuildSuccess(
        int deletedCount,
        string filterText,
        ulong targetUserId,
        int skippedOldCount,
        ulong moderatorId)
    {
        var targetLine = targetUserId == 0
            ? string.Empty
            : $"> **Target:** <@{targetUserId}>\n";

        var content =
            "## Purge Complete\n" +
            $"> **Deleted:** `{deletedCount:N0}`\n" +
            $"> **Filter:** `{filterText}`\n" +
            targetLine +
            $"> **Skipped (older than 14 days):** `{skippedOldCount:N0}`\n" +
            $"> **Moderator:** <@{moderatorId}>";

        return BuildContainer(
            new TextDisplayBuilder(content));
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
