using Discord;

namespace LastRide.Builders;

public sealed class WarnComponentBuilder
{
    private static readonly Color AccentColor = new(8, 4, 4);

    public MessageComponent BuildWarned(
        ulong targetId,
        string targetName,
        string? targetAvatarUrl,
        string reason,
        int warningCount,
        ulong moderatorId)
    {
        var content =
            $"> **Member:** <@{targetId}>\n" +
            $"> **Warnings:** `{warningCount:N0}`\n" +
            $"> **Reason:** `{EscapeInlineCode(reason)}`\n" +
            $"> **Moderator:** <@{moderatorId}>";

        return BuildContainer(
            BuildHeader("Member Warned", content, targetAvatarUrl, targetName));
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
        string? avatarUrl,
        string avatarDescription)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
        {
            return new TextDisplayBuilder(
                $"## {EscapeMarkdown(title)}\n{content}");
        }

        return new SectionBuilder()
            .WithAccessory(
                new ThumbnailBuilder(
                    new UnfurledMediaItemProperties(avatarUrl),
                    description: avatarDescription))
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
