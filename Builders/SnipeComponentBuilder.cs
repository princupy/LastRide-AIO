using Discord;
using LastRide.Models;

namespace LastRide.Builders;

public sealed class SnipeComponentBuilder
{
    private static readonly Color AccentColor = ComponentTheme.AccentColor;

    public MessageComponent Build(
        IReadOnlyList<SnipedMessage> messages,
        ulong requesterId,
        ulong channelId,
        int index)
    {
        if (messages.Count == 0)
        {
            return BuildNotice(
                "Nothing To Snipe",
                "There are no recently deleted messages in this channel.");
        }

        index = Math.Clamp(index, 0, messages.Count - 1);

        var message = messages[index];
        var attachmentLine = message.AttachmentCount == 0
            ? string.Empty
            : $"> **Attachments:** `{message.AttachmentCount:N0}`\n";

        var deletedByLine = message.DeletedById == 0
            ? "> **Deleted By:** `Unknown`\n"
            : $"> **Deleted By:** <@{message.DeletedById}>\n";

        var content =
            $"> **Author:** <@{message.AuthorId}>\n" +
            deletedByLine +
            $"> **Deleted:** <t:{message.DeletedAt.ToUnixTimeSeconds()}:R>\n" +
            attachmentLine +
            $"> **Message:** {EscapeMarkdown(message.Content)}";

        var pageLine =
            $"Showing `{index + 1}` of `{messages.Count}` recently deleted message(s).";

        return BuildContainer(
            BuildHeader(
                "Sniped Message",
                content,
                message.AuthorAvatarUrl,
                message.AuthorName),
            Divider(isDivider: false),
            new TextDisplayBuilder(pageLine),
            BuildButtons(requesterId, channelId, index, messages.Count),
            FooterSeparator(),
            new TextDisplayBuilder(ComponentFooter.Text));
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

    private static ActionRowBuilder BuildButtons(
        ulong requesterId,
        ulong channelId,
        int index,
        int totalCount)
    {
        return new ActionRowBuilder()
            .WithButton(
                ButtonBuilder.CreateSecondaryButton(
                    "Previous",
                    SnipeComponentIds.Create(
                        SnipeAction.Previous,
                        requesterId,
                        channelId,
                        index - 1))
                    .WithDisabled(index <= 0))
            .WithButton(
                ButtonBuilder.CreateSecondaryButton(
                    "Next",
                    SnipeComponentIds.Create(
                        SnipeAction.Next,
                        requesterId,
                        channelId,
                        index + 1))
                    .WithDisabled(index >= totalCount - 1));
    }

    private static SeparatorBuilder Divider(bool isDivider = true)
    {
        return new SeparatorBuilder(
            isDivider: isDivider,
            spacing: SeparatorSpacingSize.Small);
    }

    private static SeparatorBuilder FooterSeparator()
    {
        return new SeparatorBuilder(
            isDivider: true,
            spacing: SeparatorSpacingSize.Small);
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
