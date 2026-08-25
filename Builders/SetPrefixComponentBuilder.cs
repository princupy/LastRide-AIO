using Discord;

namespace LastRide.Builders;

public sealed class SetPrefixComponentBuilder
{
    private static readonly Color AccentColor = new(8, 4, 4);

    public MessageComponent BuildUpdated(
        string newPrefix,
        bool isDefault,
        bool isPersistent,
        ulong moderatorId,
        string? botAvatarUrl,
        string botName)
    {
        var title = isDefault ? "Prefix Reset" : "Prefix Updated";
        var prefixLabel = isDefault
            ? $"`{EscapeInlineCode(newPrefix)}` (default)"
            : $"`{EscapeInlineCode(newPrefix)}`";

        var lines = new List<string>
        {
            $"> **New Prefix:** {prefixLabel}",
            $"> **Example:** `{EscapeInlineCode(newPrefix)}help`",
            $"> **Updated By:** <@{moderatorId}>"
        };

        if (!isPersistent)
        {
            lines.Add(
                "> **Note:** This change is active now, but it will reset when the bot restarts.");
        }

        return BuildContainer(
            BuildHeader(
                title,
                string.Join("\n", lines),
                botAvatarUrl,
                botName));
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
