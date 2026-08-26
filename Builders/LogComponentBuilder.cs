using Discord;
using LastRide.Models;

namespace LastRide.Builders;

public sealed class LogComponentBuilder
{
    private const string EnabledEmoji = "<:Enabled:1541831759191212154>";
    private const string DisabledEmoji = "<:disabled:1541831823406014699>";
    private const int MaxContentPreview = 500;

    private static readonly Color AccentColor = new(8, 4, 4);

    // ---- Config cards (command replies) ----

    public MessageComponent BuildOverview(
        LogConfig config,
        string prefix,
        bool isPersistent,
        string? botAvatarUrl,
        string botName)
    {
        var statusEmoji = config.Enabled ? EnabledEmoji : DisabledEmoji;
        var statusText = config.Enabled ? "Enabled" : "Disabled";

        var channels = new List<string>();

        foreach (var type in Enum.GetValues<LogType>())
        {
            var channelId = config.GetChannel(type);
            var emoji = channelId is not null ? EnabledEmoji : DisabledEmoji;
            var target = channelId is { } id ? $"<#{id}>" : "`Not set`";

            channels.Add($"> {emoji} **{type.DisplayName()}** — {target}");
        }

        var body =
            $"> {statusEmoji} **Master Status:** {statusText}\n\n" +
            "### Log Channels\n" +
            string.Join("\n", channels) + "\n\n" +
            $"-# `{prefix}logset <type> #channel` to route • `{prefix}logset <type> disable` to clear • `{prefix}logenable`/`{prefix}logdisable` for the master switch";

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader("Logs Overview", body, botAvatarUrl, botName)
        };

        AppendPersistenceNote(components, isPersistent);
        AppendFooter(components);

        return BuildContainer(components.ToArray());
    }

    public MessageComponent BuildChannelResult(
        LogType type,
        ulong? channelId,
        string? note,
        bool isPersistent,
        string prefix)
    {
        var command = type.ToString().ToLowerInvariant();

        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(note))
            lines.Add($"> {note}");

        lines.Add(channelId is { } id
            ? $"> **{type.DisplayName()} Log:** <#{id}>"
            : $"> **{type.DisplayName()} Log:** `Not set`");

        lines.Add(
            $"-# `{prefix}logset {command} #channel` to set • `{prefix}logset {command} disable` to clear");

        return BuildStatusCard(
            $"{type.DisplayName()} Log Channel",
            lines,
            isPersistent);
    }

    public MessageComponent BuildMasterResult(
        bool enabled,
        bool isPersistent,
        string prefix,
        string? note = null)
    {
        var emoji = enabled ? EnabledEmoji : DisabledEmoji;
        var state = enabled ? "enabled" : "disabled";

        var lines = new List<string>
        {
            $"> {emoji} Logging is now **{state}** for this server."
        };

        if (!string.IsNullOrWhiteSpace(note))
        {
            lines.Add($"> {note}");
        }
        else if (enabled)
        {
            lines.Add(
                $"> Route each event type with `{prefix}logset <type> #channel`.");
        }

        return BuildStatusCard(
            enabled ? "Logging Enabled" : "Logging Disabled",
            lines,
            isPersistent);
    }

    /// <summary>
    /// Interactive setup card shown on <c>logenable</c>. The select menu lets a
    /// moderator pick which event types to log; the handler then creates a
    /// channel for each selected type and routes it automatically.
    /// </summary>
    public MessageComponent BuildSetupConfigurator(
        string title,
        string subtitle,
        LogConfig config,
        ulong requesterId,
        ulong guildId,
        bool isPersistent,
        string prefix)
    {
        var total = Enum.GetValues<LogType>().Length;
        var setCount = Enum.GetValues<LogType>()
            .Count(type => config.GetChannel(type) is not null);

        var statusEmoji = config.Enabled ? EnabledEmoji : DisabledEmoji;
        var statusText = config.Enabled ? "Enabled" : "Disabled";

        var channels = new List<string>();

        foreach (var type in Enum.GetValues<LogType>())
        {
            var channelId = config.GetChannel(type);
            var emoji = channelId is not null ? EnabledEmoji : DisabledEmoji;
            var target = channelId is { } id ? $"<#{id}>" : "`Not set`";

            channels.Add($"> {emoji} **{type.DisplayName()}** — {target}");
        }

        var header =
            $"## {EscapeMarkdown(title)}\n" +
            $"> {subtitle}\n" +
            $"> {statusEmoji} **Master Status:** {statusText}\n" +
            $"> **Channels set:** `{setCount}/{total}`\n\n" +
            "### Log Channels\n" +
            string.Join("\n", channels) + "\n\n" +
            $"-# Select a type and I'll create + route its channel • deselect to stop logging it • `{prefix}logset <type> #channel` to use an existing channel";

        var components = new List<IMessageComponentBuilder>
        {
            new TextDisplayBuilder(header),
            new SeparatorBuilder(
                isDivider: false,
                spacing: SeparatorSpacingSize.Small),
            BuildSetupMenu(config, requesterId, guildId)
        };

        AppendPersistenceNote(components, isPersistent);
        AppendFooter(components);

        return BuildContainer(components.ToArray());
    }

    private static ActionRowBuilder BuildSetupMenu(
        LogConfig config,
        ulong requesterId,
        ulong guildId)
    {
        var menu = new SelectMenuBuilder()
            .WithCustomId(LogComponentIds.CreateSetupMenu(requesterId, guildId))
            .WithPlaceholder("Select the logs to enable")
            .WithMinValues(0)
            .WithMaxValues(Enum.GetValues<LogType>().Length);

        foreach (var type in Enum.GetValues<LogType>())
        {
            menu.AddOption(
                type.DisplayName(),
                type.ToString(),
                MenuHint(type),
                isDefault: config.GetChannel(type) is not null);
        }

        return new ActionRowBuilder().WithSelectMenu(menu);
    }

    private static string MenuHint(LogType type)
    {
        return type switch
        {
            LogType.Messages => "Deleted, edited & bulk-deleted messages",
            LogType.Members => "Member joins & leaves",
            LogType.Voice => "Voice join, leave & move",
            LogType.Moderation => "Bans, unbans, kicks, mutes & warns",
            LogType.Roles => "Member role adds & removes",
            LogType.Server => "Channel & role create, delete & rename",
            _ => string.Empty
        };
    }

    public MessageComponent BuildNotice(string title, string message)
    {
        return BuildContainer(
            new TextDisplayBuilder(
                $"## {EscapeMarkdown(title)}\n> {message}"));
    }

    // ---- Event-log cards (posted to configured channels) ----

    public MessageComponent BuildMessageDeletedLog(
        ulong authorId,
        string authorName,
        string? avatarUrl,
        ulong channelId,
        string content,
        int attachmentCount,
        ulong? deleterId)
    {
        var preview = string.IsNullOrWhiteSpace(content)
            ? "*No text content.*"
            : $"`{EscapeInlineCode(Truncate(content, MaxContentPreview))}`";

        // A resolved deleter that isn't the author means a moderator/bot removed
        // it; otherwise the author deleted their own message (no audit entry).
        var deletedBy = deleterId is { } id && id != authorId
            ? $"<@{id}>"
            : $"<@{authorId}> (self)";

        var body =
            $"> **Author:** <@{authorId}> (`{EscapeInlineCode(authorName)}`)\n" +
            $"> **Deleted By:** {deletedBy}\n" +
            $"> **Channel:** <#{channelId}>\n" +
            $"> **Content:** {preview}";

        if (attachmentCount > 0)
            body += $"\n> **Attachments:** `{attachmentCount}`";

        return BuildEventCard("Message Deleted", body, avatarUrl, authorName);
    }

    public MessageComponent BuildMessageEditedLog(
        ulong authorId,
        string authorName,
        string? avatarUrl,
        ulong channelId,
        string before,
        string after)
    {
        var beforeText = string.IsNullOrWhiteSpace(before)
            ? "*No text content.*"
            : $"`{EscapeInlineCode(Truncate(before, MaxContentPreview))}`";
        var afterText = string.IsNullOrWhiteSpace(after)
            ? "*No text content.*"
            : $"`{EscapeInlineCode(Truncate(after, MaxContentPreview))}`";

        var body =
            $"> **Author:** <@{authorId}> (`{EscapeInlineCode(authorName)}`)\n" +
            $"> **Channel:** <#{channelId}>\n" +
            $"> **Before:** {beforeText}\n" +
            $"> **After:** {afterText}";

        return BuildEventCard("Message Edited", body, avatarUrl, authorName);
    }

    public MessageComponent BuildBulkDeleteLog(ulong channelId, int count)
    {
        var body =
            $"> **Channel:** <#{channelId}>\n" +
            $"> **Messages Deleted:** `{count:N0}`";

        return BuildEventCard("Bulk Message Delete", body, null, string.Empty);
    }

    public MessageComponent BuildMemberJoinLog(
        ulong userId,
        string userName,
        string? avatarUrl,
        DateTimeOffset? accountCreated,
        int memberCount)
    {
        var body =
            $"> **Member:** <@{userId}> (`{EscapeInlineCode(userName)}`)\n" +
            $"> **Account Created:** {FormatTimestamp(accountCreated)}\n" +
            $"> **Member Count:** `{memberCount:N0}`";

        return BuildEventCard("Member Joined", body, avatarUrl, userName);
    }

    public MessageComponent BuildMemberLeaveLog(
        ulong userId,
        string userName,
        string? avatarUrl,
        DateTimeOffset? joinedAt,
        int memberCount)
    {
        var body =
            $"> **Member:** <@{userId}> (`{EscapeInlineCode(userName)}`)\n" +
            $"> **Joined:** {FormatTimestamp(joinedAt)}\n" +
            $"> **Member Count:** `{memberCount:N0}`";

        return BuildEventCard("Member Left", body, avatarUrl, userName);
    }

    public MessageComponent BuildVoiceLog(
        string action,
        ulong userId,
        string userName,
        string? avatarUrl,
        string? fromChannel,
        string? toChannel)
    {
        var lines = new List<string>
        {
            $"> **Member:** <@{userId}> (`{EscapeInlineCode(userName)}`)"
        };

        if (fromChannel is not null)
            lines.Add($"> **From:** {EscapeMarkdown(fromChannel)}");

        if (toChannel is not null)
            lines.Add($"> **To:** {EscapeMarkdown(toChannel)}");

        return BuildEventCard(
            $"Voice {action}",
            string.Join("\n", lines),
            avatarUrl,
            userName);
    }

    public MessageComponent BuildModerationLog(
        string action,
        ulong targetId,
        string targetName,
        string? avatarUrl,
        ulong? moderatorId,
        string? reason,
        string? extra)
    {
        var lines = new List<string>
        {
            $"> **User:** <@{targetId}> (`{EscapeInlineCode(targetName)}`)",
            moderatorId is { } modId
                ? $"> **Moderator:** <@{modId}>"
                : "> **Moderator:** `Unknown`"
        };

        if (!string.IsNullOrWhiteSpace(extra))
            lines.Add($"> {extra}");

        lines.Add(
            $"> **Reason:** {(string.IsNullOrWhiteSpace(reason) ? "`No reason provided.`" : EscapeMarkdown(reason))}");

        return BuildEventCard(
            $"Member {action}",
            string.Join("\n", lines),
            avatarUrl,
            targetName);
    }

    public MessageComponent BuildRoleUpdateLog(
        ulong userId,
        string userName,
        string? avatarUrl,
        IReadOnlyList<ulong> addedRoleIds,
        IReadOnlyList<ulong> removedRoleIds,
        ulong? moderatorId)
    {
        var lines = new List<string>
        {
            $"> **Member:** <@{userId}> (`{EscapeInlineCode(userName)}`)"
        };

        if (addedRoleIds.Count > 0)
            lines.Add($"> **Added:** {string.Join(" ", addedRoleIds.Select(id => $"<@&{id}>"))}");

        if (removedRoleIds.Count > 0)
            lines.Add($"> **Removed:** {string.Join(" ", removedRoleIds.Select(id => $"<@&{id}>"))}");

        lines.Add(moderatorId is { } modId
            ? $"> **By:** <@{modId}>"
            : "> **By:** `Unknown`");

        return BuildEventCard(
            "Roles Updated",
            string.Join("\n", lines),
            avatarUrl,
            userName);
    }

    public MessageComponent BuildChannelCreatedLog(
        ulong channelId,
        string channelType,
        ulong? moderatorId)
    {
        return BuildServerCard(
            "Channel Created",
            new List<string>
            {
                $"> **Channel:** <#{channelId}>",
                $"> **Type:** `{EscapeInlineCode(channelType)}`"
            },
            moderatorId);
    }

    public MessageComponent BuildChannelDeletedLog(
        string channelName,
        string channelType,
        ulong? moderatorId)
    {
        return BuildServerCard(
            "Channel Deleted",
            new List<string>
            {
                $"> **Channel:** `{EscapeInlineCode(channelName)}`",
                $"> **Type:** `{EscapeInlineCode(channelType)}`"
            },
            moderatorId);
    }

    public MessageComponent BuildChannelRenamedLog(
        ulong channelId,
        string before,
        string after,
        ulong? moderatorId)
    {
        return BuildServerCard(
            "Channel Renamed",
            new List<string>
            {
                $"> **Channel:** <#{channelId}>",
                $"> **Before:** `{EscapeInlineCode(before)}`",
                $"> **After:** `{EscapeInlineCode(after)}`"
            },
            moderatorId);
    }

    public MessageComponent BuildRoleCreatedLog(
        ulong roleId,
        string roleName,
        ulong? moderatorId)
    {
        return BuildServerCard(
            "Role Created",
            new List<string>
            {
                $"> **Role:** <@&{roleId}> (`{EscapeInlineCode(roleName)}`)"
            },
            moderatorId);
    }

    public MessageComponent BuildRoleDeletedLog(
        string roleName,
        ulong? moderatorId)
    {
        return BuildServerCard(
            "Role Deleted",
            new List<string>
            {
                $"> **Role:** `{EscapeInlineCode(roleName)}`"
            },
            moderatorId);
    }

    public MessageComponent BuildRoleRenamedLog(
        ulong roleId,
        string before,
        string after,
        ulong? moderatorId)
    {
        return BuildServerCard(
            "Role Renamed",
            new List<string>
            {
                $"> **Role:** <@&{roleId}>",
                $"> **Before:** `{EscapeInlineCode(before)}`",
                $"> **After:** `{EscapeInlineCode(after)}`"
            },
            moderatorId);
    }

    // ---- Shared helpers ----

    private MessageComponent BuildEventCard(
        string title,
        string body,
        string? avatarUrl,
        string avatarDescription)
    {
        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader(title, body, avatarUrl, avatarDescription)
        };

        AppendFooter(components);

        return BuildContainer(components.ToArray());
    }

    private MessageComponent BuildServerCard(
        string title,
        List<string> lines,
        ulong? moderatorId)
    {
        lines.Add(moderatorId is { } modId
            ? $"> **By:** <@{modId}>"
            : "> **By:** `Unknown`");

        return BuildEventCard(
            title,
            string.Join("\n", lines),
            null,
            string.Empty);
    }

    private MessageComponent BuildStatusCard(
        string title,
        List<string> lines,
        bool isPersistent)
    {
        var components = new List<IMessageComponentBuilder>
        {
            new TextDisplayBuilder(
                $"## {EscapeMarkdown(title)}\n{string.Join("\n", lines)}")
        };

        AppendPersistenceNote(components, isPersistent);
        AppendFooter(components);

        return BuildContainer(components.ToArray());
    }

    private static void AppendPersistenceNote(
        List<IMessageComponentBuilder> components,
        bool isPersistent)
    {
        if (isPersistent)
            return;

        components.Add(new TextDisplayBuilder(
            "-# Note: settings are active now but will reset when the bot restarts."));
    }

    private static void AppendFooter(List<IMessageComponentBuilder> components)
    {
        components.Add(new SeparatorBuilder(
            isDivider: true,
            spacing: SeparatorSpacingSize.Small));
        components.Add(new TextDisplayBuilder(ComponentFooter.Text));
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

    private static string FormatTimestamp(DateTimeOffset? time)
    {
        if (time is not { } value)
            return "`Unknown`";

        var unix = value.ToUnixTimeSeconds();
        return $"<t:{unix}:f> (<t:{unix}:R>)";
    }

    private static string Truncate(string value, int maxLength)
    {
        var collapsed = value.Replace("\n", " ").Replace("\r", " ").Trim();

        return collapsed.Length <= maxLength
            ? collapsed
            : collapsed[..(maxLength - 1)] + "…";
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
