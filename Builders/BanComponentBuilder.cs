using Discord;
using LastRide.Models;

namespace LastRide.Builders;

public sealed class BanComponentBuilder
{
    private static readonly Color AccentColor = ComponentTheme.AccentColor;

    public MessageComponent BuildPrompt(PendingBanRequest request)
    {
        var content =
            $"> **Target:** `{EscapeInlineCode(request.TargetName)}`\n" +
            $"> **Target ID:** `{request.TargetId}`\n" +
            $"> **Reason:** `{EscapeInlineCode(request.Reason)}`\n" +
            "> This action will ban the user from this server.";

        return BuildContainer(
            BuildHeader("Confirm Ban", content, request.TargetAvatarUrl, request.TargetName),
            Divider(isDivider: false),
            BuildButtons(request.Id),
            FooterSeparator(),
            new TextDisplayBuilder(ComponentFooter.Text));
    }

    public MessageComponent BuildSuccess(PendingBanRequest request)
    {
        return BuildNotice(
            "Ban Complete",
            $"`{EscapeInlineCode(request.TargetName)}` has been banned. Reason: `{EscapeInlineCode(request.Reason)}`");
    }

    public MessageComponent BuildCancelled(PendingBanRequest request)
    {
        return BuildNotice(
            "Ban Cancelled",
            $"Ban request for `{EscapeInlineCode(request.TargetName)}` was cancelled.");
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

    private static ActionRowBuilder BuildButtons(string requestId)
    {
        return new ActionRowBuilder()
            .WithButton(
                ButtonBuilder.CreateDangerButton(
                    "Confirm Ban",
                    BanComponentIds.Create(BanAction.Confirm, requestId)))
            .WithButton(
                ButtonBuilder.CreateSecondaryButton(
                    "Cancel",
                    BanComponentIds.Create(BanAction.Cancel, requestId)));
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
