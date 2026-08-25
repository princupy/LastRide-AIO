using Discord;

namespace LastRide.Builders;

public sealed class RoleIconComponentBuilder
{
    private static readonly Color AccentColor = new(8, 4, 4);

    public MessageComponent BuildSuccess(
        string roleName,
        ulong roleId,
        string iconText,
        string? iconUrl,
        ulong moderatorId)
    {
        var content =
            $"> **Role:** `{EscapeInlineCode(roleName)}`\n" +
            $"> **Role ID:** `{roleId}`\n" +
            $"> **Icon:** {iconText}\n" +
            $"> **Moderator:** <@{moderatorId}>";

        return BuildContainer(
            BuildHeader("Role Icon Updated", content, iconUrl, roleName));
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
        string? iconUrl,
        string iconDescription)
    {
        if (string.IsNullOrWhiteSpace(iconUrl))
        {
            return new TextDisplayBuilder(
                $"## {EscapeMarkdown(title)}\n{content}");
        }

        return new SectionBuilder()
            .WithAccessory(
                new ThumbnailBuilder(
                    new UnfurledMediaItemProperties(iconUrl),
                    description: iconDescription))
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
