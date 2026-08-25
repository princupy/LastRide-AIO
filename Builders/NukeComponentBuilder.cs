using Discord;
using LastRide.Models;

namespace LastRide.Builders;

public sealed class NukeComponentBuilder
{
    private static readonly Color AccentColor = new(8, 4, 4);

    public MessageComponent BuildPrompt(PendingNukeRequest request)
    {
        var content =
            $"## Confirm Nuke\n" +
            $"> **Channel:** `{EscapeInlineCode(request.ChannelName)}`\n" +
            $"> This will delete this channel and recreate an identical copy.\n" +
            $"> **All messages in it will be permanently lost.**";

        return BuildContainer(
            new TextDisplayBuilder(content),
            Divider(isDivider: false),
            BuildButtons(request.Id),
            FooterSeparator(),
            new TextDisplayBuilder(ComponentFooter.Text));
    }

    public MessageComponent BuildSuccess(ulong moderatorId)
    {
        var content =
            "## Channel Nuked\n" +
            $"> This channel has been nuked and recreated by <@{moderatorId}>.";

        return BuildContainer(
            new TextDisplayBuilder(content));
    }

    public MessageComponent BuildCancelled(PendingNukeRequest request)
    {
        return BuildNotice(
            "Nuke Cancelled",
            $"Nuke request for `{EscapeInlineCode(request.ChannelName)}` was cancelled.");
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

    private static ActionRowBuilder BuildButtons(string requestId)
    {
        return new ActionRowBuilder()
            .WithButton(
                ButtonBuilder.CreateDangerButton(
                    "Confirm Nuke",
                    NukeComponentIds.Create(NukeAction.Confirm, requestId)))
            .WithButton(
                ButtonBuilder.CreateSecondaryButton(
                    "Cancel",
                    NukeComponentIds.Create(NukeAction.Cancel, requestId)));
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
