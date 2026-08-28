using Discord;
using Discord.WebSocket;

namespace LastRide.Builders;

public sealed class UnlockAllComponentBuilder
{
    private const int MaxChannelOptions = 24;
    private static readonly Color AccentColor = ComponentTheme.AccentColor;

    public MessageComponent BuildMenu(
        SocketGuild guild,
        ulong requesterId)
    {
        var lockedChannels = GetLockedChannels(guild);

        if (lockedChannels.Length == 0)
        {
            return BuildNotice(
                "No Locked Channels",
                "There are no locked channels to unlock.");
        }

        var shownChannels = lockedChannels
            .Take(MaxChannelOptions)
            .ToArray();
        var lockedCountText = lockedChannels.Length == shownChannels.Length
            ? $"`{lockedChannels.Length:N0}` locked channel(s) found."
            : $"`{lockedChannels.Length:N0}` locked channel(s) found. Showing the first `{shownChannels.Length:N0}`.";

        var content =
            "## Unlock Channels\n" +
            $"> {lockedCountText}\n" +
            "> Select channels to unlock, or choose `Unlock All`.";

        if (lockedChannels.Length > shownChannels.Length)
        {
            content +=
                "\n> Use `Unlock All` to unlock every locked channel in this server.";
        }

        var menu = new SelectMenuBuilder()
            .WithCustomId(
                UnlockAllComponentIds.CreateMenu(
                    requesterId,
                    guild.Id))
            .WithPlaceholder("Select locked channels to unlock")
            .WithMinValues(1)
            .WithMaxValues(shownChannels.Length + 1)
            .AddOption(
                "Unlock All",
                UnlockAllComponentIds.AllChannelsValue,
                "Unlock every locked channel in this server");

        foreach (var channel in shownChannels)
        {
            menu.AddOption(
                Trim(channel.Name, 100),
                UnlockAllComponentIds.CreateChannelValue(channel.Id),
                BuildChannelDescription(channel));
        }

        return BuildContainer(
            new TextDisplayBuilder(content),
            Divider(),
            new ActionRowBuilder().WithSelectMenu(menu));
    }

    public MessageComponent BuildSuccess(
        int unlockedCount,
        int skippedCount,
        int failedCount,
        ulong moderatorId)
    {
        var content =
            "## Unlock All Complete\n" +
            $"> **Unlocked:** `{unlockedCount:N0}`\n" +
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

    public static SocketGuildChannel[] GetLockedChannels(SocketGuild guild)
    {
        var everyoneRole = guild.EveryoneRole;

        return guild.Channels
            .Where(channel =>
                channel.GetPermissionOverwrite(everyoneRole)?.SendMessages ==
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
