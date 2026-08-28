using Discord;

namespace LastRide.Builders;

public sealed class UnhideComponentBuilder
{
    private static readonly Color AccentColor = ComponentTheme.AccentColor;

    public MessageComponent BuildSuccess(
        ulong channelId,
        ulong moderatorId)
    {
        return BuildContainer(
            new TextDisplayBuilder(
                $"<#{channelId}> has been unhidden by <@{moderatorId}>."));
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
