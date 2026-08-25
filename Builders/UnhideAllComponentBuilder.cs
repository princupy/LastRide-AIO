using Discord;
using Discord.WebSocket;

namespace LastRide.Builders;

public sealed class UnhideAllComponentBuilder
{
    private const int MaxChannelOptions = 24;
    private static readonly Color AccentColor = new(8, 4, 4);

    public MessageComponent BuildMenu(
        SocketGuild guild,
        ulong requesterId)
    {
        var hiddenChannels = GetHiddenChannels(guild);

        if (hiddenChannels.Length == 0)
        {
            return BuildNotice(
                "No Hidden Channels",
                "There are no hidden channels to unhide.");
        }

        var shownChannels = hiddenChannels
            .Take(MaxChannelOptions)
            .ToArray();
        var hiddenCountText = hiddenChannels.Length == shownChannels.Length
            ? $"`{hiddenChannels.Length:N0}` hidden channel(s) found."
            : $"`{hiddenChannels.Length:N0}` hidden channel(s) found. Showing the first `{shownChannels.Length:N0}`.";

        var content =
            "## Unhide Channels\n" +
            $"> {hiddenCountText}\n" +
            "> Select channels to unhide, or choose `Unhide All`.";

        if (hiddenChannels.Length > shownChannels.Length)
        {
            content +=
                "\n> Use `Unhide All` to unhide every hidden channel in this server.";
        }

        var menu = new SelectMenuBuilder()
            .WithCustomId(
                UnhideAllComponentIds.CreateMenu(
                    requesterId,
                    guild.Id))
            .WithPlaceholder("Select hidden channels to unhide")
            .WithMinValues(1)
            .WithMaxValues(shownChannels.Length + 1)
            .AddOption(
                "Unhide All",
                UnhideAllComponentIds.AllChannelsValue,
                "Unhide every hidden channel in this server");

        foreach (var channel in shownChannels)
        {
            menu.AddOption(
                Trim(channel.Name, 100),
                UnhideAllComponentIds.CreateChannelValue(channel.Id),
                BuildChannelDescription(channel));
        }

        return BuildContainer(
            new TextDisplayBuilder(content),
            Divider(),
            new ActionRowBuilder().WithSelectMenu(menu));
    }

    public MessageComponent BuildSuccess(
        int unhiddenCount,
        int skippedCount,
        int failedCount,
        ulong moderatorId)
    {
        var content =
            "## Unhide All Complete\n" +
            $"> **Unhidden:** `{unhiddenCount:N0}`\n" +
            $"> **Skipped:** `{skippedCount:N0}`\n" +
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

    public static SocketGuildChannel[] GetHiddenChannels(SocketGuild guild)
    {
        var everyoneRole = guild.EveryoneRole;

        return guild.Channels
            .Where(channel =>
                channel.GetPermissionOverwrite(everyoneRole)?.ViewChannel ==
                PermValue.Deny)
            .OrderBy(channel => channel.Position)
            .ThenBy(channel => channel.Name)
            .ToArray();
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

    private static SeparatorBuilder Divider()
    {
        return new SeparatorBuilder(
            isDivider: true,
            spacing: SeparatorSpacingSize.Small);
    }

    private static string BuildChannelDescription(SocketGuildChannel channel)
    {
        return Trim($"{GetChannelKind(channel)} - ID: {channel.Id}", 100);
    }

    private static string GetChannelKind(SocketGuildChannel channel)
    {
        return channel switch
        {
            SocketStageChannel => "Stage channel",
            SocketVoiceChannel => "Voice channel",
            SocketForumChannel => "Forum channel",
            SocketTextChannel => "Text channel",
            SocketCategoryChannel => "Category",
            _ => "Channel"
        };
    }

    private static string Trim(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        return value[..Math.Max(0, maxLength - 3)] + "...";
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
