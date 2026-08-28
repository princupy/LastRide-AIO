using Discord;
using Discord.WebSocket;

namespace LastRide.Builders;

public sealed class VoiceComponentBuilder
{
    private const int MaxChannelOptions = 25;
    private const int MaxListedMembers = 40;
    private static readonly Color AccentColor = ComponentTheme.AccentColor;

    // Single-target result card (mute, deafen, disconnect, move, pull).
    public MessageComponent BuildActionResult(
        string title,
        ulong memberId,
        ulong moderatorId,
        string? detail = null)
    {
        var lines = new List<string>
        {
            $"> **Member:** <@{memberId}>"
        };

        if (!string.IsNullOrEmpty(detail))
            lines.Add($"> {detail}");

        lines.Add($"> **Moderator:** <@{moderatorId}>");

        return BuildContainer(
            new TextDisplayBuilder(
                $"## {EscapeMarkdown(title)}\n{string.Join("\n", lines)}"));
    }

    // Channel-level result card (lock, unlock, hide, unhide).
    public MessageComponent BuildChannelResult(
        string title,
        ulong channelId,
        ulong moderatorId)
    {
        var content =
            $"## {EscapeMarkdown(title)}\n" +
            $"> **Channel:** <#{channelId}>\n" +
            $"> **Moderator:** <@{moderatorId}>";

        return BuildContainer(new TextDisplayBuilder(content));
    }

    // Bulk result card for the *all commands.
    public MessageComponent BuildBulkResult(
        string title,
        int affected,
        int skipped,
        int failed,
        ulong moderatorId,
        string channelName)
    {
        var content =
            $"## {EscapeMarkdown(title)}\n" +
            $"> **Channel:** {EscapeMarkdown(channelName)}\n" +
            $"> **Affected:** `{affected:N0}`\n" +
            $"> **Skipped:** `{skipped:N0}`\n" +
            $"> **Failed:** `{failed:N0}`\n" +
            $"> **Moderator:** <@{moderatorId}>";

        return BuildContainer(new TextDisplayBuilder(content));
    }

    // vclist — the requester's current voice channel and each member's state.
    public MessageComponent BuildList(
        SocketVoiceChannel channel,
        ulong requesterId)
    {
        var members = channel.ConnectedUsers
            .OrderBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var lines = new List<string>
        {
            $"> **Channel:** {EscapeMarkdown(channel.Name)} • **Members:** `{members.Length:N0}`"
        };

        if (members.Length == 0)
        {
            lines.Add("> No one is connected right now.");
        }
        else
        {
            var index = 1;

            foreach (var member in members.Take(MaxListedMembers))
            {
                lines.Add($"> **{index}.** <@{member.Id}> — {DescribeState(member)}");
                index++;
            }

            if (members.Length > MaxListedMembers)
                lines.Add($"> …and `{members.Length - MaxListedMembers:N0}` more.");
        }

        return BuildContainer(
            new TextDisplayBuilder(
                $"## Voice Members\n{string.Join("\n", lines)}"));
    }

    // Voice-channel picker for move / moveall / pullall. The chosen channel is
    // resolved live when the select fires, so nothing is captured here beyond
    // the ids encoded in the custom id.
    public MessageComponent BuildMoveMenu(
        SocketGuild guild,
        string op,
        ulong requesterId,
        ulong targetId,
        ulong excludeChannelId)
    {
        var channels = guild.VoiceChannels
            .Where(channel => channel.Id != excludeChannelId)
            .OrderBy(channel => channel.Position)
            .ThenBy(channel => channel.Name)
            .Take(MaxChannelOptions)
            .ToArray();

        if (channels.Length == 0)
        {
            return BuildNotice(
                "No Voice Channels",
                "There are no other voice channels available.");
        }

        var (heading, description, placeholder) = op switch
        {
            "move" =>
                ("Move Member",
                    $"Select the voice channel to move <@{targetId}> to.",
                    "Select destination channel"),
            "moveall" =>
                ("Move Members",
                    "Select the destination — everyone in your current channel will be moved.",
                    "Select destination channel"),
            _ =>
                ("Pull Members",
                    "Select the channel to pull everyone from into your channel.",
                    "Select source channel")
        };

        var menu = new SelectMenuBuilder()
            .WithCustomId(
                VoiceComponentIds.CreateMenu(op, requesterId, guild.Id, targetId))
            .WithPlaceholder(placeholder)
            .WithMinValues(1)
            .WithMaxValues(1);

        foreach (var channel in channels)
        {
            menu.AddOption(
                Trim(channel.Name, 100),
                VoiceComponentIds.CreateChannelValue(channel.Id),
                Trim($"Voice channel • {channel.ConnectedUsers.Count:N0} member(s)", 100));
        }

        return BuildContainer(
            new TextDisplayBuilder($"## {heading}\n> {description}"),
            Divider(),
            new ActionRowBuilder().WithSelectMenu(menu));
    }

    public MessageComponent BuildNotice(string title, string message)
    {
        return BuildContainer(
            new TextDisplayBuilder(
                $"## {EscapeMarkdown(title)}\n> {message}"));
    }

    // Human-readable voice state; server states take priority over self states.
    private static string DescribeState(SocketGuildUser member)
    {
        var tags = new List<string>();

        if (member.IsMuted)
            tags.Add("server-muted");

        if (member.IsDeafened)
            tags.Add("server-deafened");

        if (member.IsSelfMuted && !member.IsMuted)
            tags.Add("muted");

        if (member.IsSelfDeafened && !member.IsDeafened)
            tags.Add("deafened");

        if (member.IsStreaming)
            tags.Add("streaming");

        if (member.IsVideoing)
            tags.Add("video");

        return tags.Count == 0
            ? "active"
            : string.Join(", ", tags);
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
