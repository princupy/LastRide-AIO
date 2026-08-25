using Discord;

namespace LastRide.Builders;

public sealed class NickComponentBuilder
{
    private static readonly Color AccentColor = new(8, 4, 4);

    public MessageComponent BuildChanged(
        ulong targetId,
        string targetName,
        string? targetAvatarUrl,
        string? oldNickname,
        string? newNickname,
        ulong moderatorId)
    {
        var content =
            $"> **Member:** <@{targetId}>\n" +
            $"> **Before:** `{FormatNickname(oldNickname)}`\n" +
            $"> **After:** `{FormatNickname(newNickname)}`\n" +
            $"> **Moderator:** <@{moderatorId}>";

        return BuildContainer(
            BuildHeader("Nickname Updated", content, targetAvatarUrl, targetName));
    }

    public MessageComponent BuildNotice(
        string title,
        string message)
    {
        return BuildContainer(
            new TextDisplayBuilder(
                $"## {EscapeMarkdown(title)}\n> {message}"));
    }

    private static string FormatNickname(string? nickname)
    {
        return string.IsNullOrWhiteSpace(nickname)
            ? "None"
            : EscapeInlineCode(nickname);
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
