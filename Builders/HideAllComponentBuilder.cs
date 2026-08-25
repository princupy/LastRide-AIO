using Discord;

namespace LastRide.Builders;

public sealed class HideAllComponentBuilder
{
    private static readonly Color AccentColor = new(8, 4, 4);

    public MessageComponent BuildSuccess(
        int hiddenCount,
        int alreadyHiddenCount,
        int failedCount,
        ulong moderatorId)
    {
        var content =
            "## Hide All Complete\n" +
            $"> **Hidden:** `{hiddenCount:N0}`\n" +
            $"> **Already Hidden:** `{alreadyHiddenCount:N0}`\n" +
            $"> **Failed:** `{failedCount:N0}`\n" +
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
