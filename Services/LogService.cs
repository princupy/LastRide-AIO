using Discord;
using Discord.Rest;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Core;
using LastRide.Models;

namespace LastRide.Services;

/// <summary>
/// Passive logging service. Exposes gateway-event handlers (wired up in
/// <see cref="Core.CommandHandler"/>) and explicit hooks for bot-initiated
/// moderation actions. Each handler resolves the guild, checks the master
/// switch, finds the configured channel for the event's <see cref="LogType"/>,
/// and posts a component card there.
/// </summary>
public sealed class LogService
{
    private static readonly TimeSpan AuditLogLookupDelay =
        TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan AuditLogMaxAge =
        TimeSpan.FromSeconds(15);
    private const int AuditLogLookupLimit = 5;

    private readonly LogConfigService _configService;
    private readonly LogComponentBuilder _builder;

    public LogService(
        LogConfigService configService,
        LogComponentBuilder builder)
    {
        _configService = configService;
        _builder = builder;
    }

    // ---- Messages ----

    public async Task HandleMessageDeletedAsync(
        Cacheable<IMessage, ulong> cacheable,
        Cacheable<IMessageChannel, ulong> channelCacheable)
    {
        if (!cacheable.HasValue)
            return;

        if (cacheable.Value is not IUserMessage message)
            return;

        if (message.Author.IsBot || message.Author.IsWebhook)
            return;

        if (message.Channel is not SocketGuildChannel guildChannel)
            return;

        var channel = ResolveLogChannel(guildChannel.Guild, LogType.Messages);

        if (channel is null)
            return;

        var authorName = message.Author is IGuildUser guildUser
            ? guildUser.DisplayName
            : message.Author.Username;

        var deleterId = await ResolveMessageDeleterAsync(
            guildChannel.Guild, message.Channel.Id, message.Author.Id);

        await SendAsync(channel, _builder.BuildMessageDeletedLog(
            message.Author.Id,
            authorName,
            message.Author.GetDisplayAvatarUrl(size: 256),
            message.Channel.Id,
            message.Content,
            message.Attachments.Count,
            deleterId));
    }

    public async Task HandleMessageUpdatedAsync(
        Cacheable<IMessage, ulong> before,
        SocketMessage after,
        ISocketMessageChannel channel)
    {
        if (after is not IUserMessage message)
            return;

        if (message.Author.IsBot || message.Author.IsWebhook)
            return;

        if (channel is not SocketGuildChannel guildChannel)
            return;

        // Only log edits we can show a diff for; skip embed-load re-emits and
        // other uncached churn to keep the channel clean.
        if (!before.HasValue)
            return;

        var beforeContent = before.Value.Content;
        var afterContent = message.Content;

        if (string.Equals(beforeContent, afterContent, StringComparison.Ordinal))
            return;

        var logChannel = ResolveLogChannel(guildChannel.Guild, LogType.Messages);

        if (logChannel is null)
            return;

        var authorName = message.Author is IGuildUser guildUser
            ? guildUser.DisplayName
            : message.Author.Username;

        await SendAsync(logChannel, _builder.BuildMessageEditedLog(
            message.Author.Id,
            authorName,
            message.Author.GetDisplayAvatarUrl(size: 256),
            channel.Id,
            beforeContent,
            afterContent));
    }

    public async Task HandleMessagesBulkDeletedAsync(
        IReadOnlyCollection<Cacheable<IMessage, ulong>> messages,
        Cacheable<IMessageChannel, ulong> channelCacheable)
    {
        if (channelCacheable.Value is not SocketGuildChannel guildChannel)
            return;

        var channel = ResolveLogChannel(guildChannel.Guild, LogType.Messages);

        if (channel is null)
            return;

        await SendAsync(channel, _builder.BuildBulkDeleteLog(
            channelCacheable.Id,
            messages.Count));
    }

    // ---- Members ----

    public async Task HandleUserJoinedAsync(SocketGuildUser user)
    {
        var channel = ResolveLogChannel(user.Guild, LogType.Members);

        if (channel is null)
            return;

        await SendAsync(channel, _builder.BuildMemberJoinLog(
            user.Id,
            user.Username,
            user.GetDisplayAvatarUrl(size: 256),
            user.CreatedAt,
            user.Guild.MemberCount));
    }

    public async Task HandleUserLeftAsync(SocketGuild guild, SocketUser user)
    {
        var channel = ResolveLogChannel(guild, LogType.Members);

        if (channel is null)
            return;

        var joinedAt = (user as SocketGuildUser)?.JoinedAt;

        await SendAsync(channel, _builder.BuildMemberLeaveLog(
            user.Id,
            user.Username,
            user.GetDisplayAvatarUrl(size: 256),
            joinedAt,
            guild.MemberCount));
    }

    // ---- Voice ----

    public async Task HandleVoiceStateUpdatedAsync(
        SocketUser user,
        SocketVoiceState before,
        SocketVoiceState after)
    {
        if (user is not SocketGuildUser guildUser)
            return;

        var channel = ResolveLogChannel(guildUser.Guild, LogType.Voice);

        if (channel is null)
            return;

        var from = before.VoiceChannel;
        var to = after.VoiceChannel;

        // Ignore state-only changes (mute/deafen/stream) where the channel is
        // unchanged — only join/leave/move are logged.
        if (from?.Id == to?.Id)
            return;

        var avatarUrl = guildUser.GetDisplayAvatarUrl(size: 256);

        MessageComponent card;

        if (from is null)
        {
            card = _builder.BuildVoiceLog(
                "Joined", guildUser.Id, guildUser.Username, avatarUrl,
                null, to?.Name);
        }
        else if (to is null)
        {
            card = _builder.BuildVoiceLog(
                "Left", guildUser.Id, guildUser.Username, avatarUrl,
                from.Name, null);
        }
        else
        {
            card = _builder.BuildVoiceLog(
                "Moved", guildUser.Id, guildUser.Username, avatarUrl,
                from.Name, to.Name);
        }

        await SendAsync(channel, card);
    }

    // ---- Moderation: ban/unban (gateway + audit-log attribution) ----

    public async Task HandleUserBannedAsync(SocketUser user, SocketGuild guild)
    {
        var channel = ResolveLogChannel(guild, LogType.Moderation);

        if (channel is null)
            return;

        var (moderatorId, reason) = await ResolveModeratorAsync(
            guild, user.Id, ActionType.Ban);

        await SendAsync(channel, _builder.BuildModerationLog(
            "Banned",
            user.Id,
            user.Username,
            user.GetDisplayAvatarUrl(size: 256),
            moderatorId,
            reason,
            extra: null));
    }

    public async Task HandleUserUnbannedAsync(SocketUser user, SocketGuild guild)
    {
        var channel = ResolveLogChannel(guild, LogType.Moderation);

        if (channel is null)
            return;

        var (moderatorId, reason) = await ResolveModeratorAsync(
            guild, user.Id, ActionType.Unban);

        await SendAsync(channel, _builder.BuildModerationLog(
            "Unbanned",
            user.Id,
            user.Username,
            user.GetDisplayAvatarUrl(size: 256),
            moderatorId,
            reason,
            extra: null));
    }

    // ---- Moderation: explicit bot-initiated hooks ----

    public Task LogKickAsync(
        SocketGuild guild,
        SocketGuildUser target,
        SocketGuildUser moderator,
        string reason)
    {
        return LogModerationAsync(
            guild,
            "Kicked",
            target.Id,
            target.Username,
            target.GetDisplayAvatarUrl(size: 256),
            moderator.Id,
            reason,
            extra: null);
    }

    public Task LogMuteAsync(
        SocketGuild guild,
        SocketGuildUser target,
        SocketGuildUser moderator,
        TimeSpan duration,
        string reason)
    {
        var expires = DateTimeOffset.UtcNow.Add(duration).ToUnixTimeSeconds();

        return LogModerationAsync(
            guild,
            "Muted",
            target.Id,
            target.Username,
            target.GetDisplayAvatarUrl(size: 256),
            moderator.Id,
            reason,
            extra: $"**Expires:** <t:{expires}:R>");
    }

    public Task LogWarnAsync(
        SocketGuild guild,
        SocketGuildUser target,
        SocketGuildUser moderator,
        string reason,
        int count)
    {
        return LogModerationAsync(
            guild,
            "Warned",
            target.Id,
            target.Username,
            target.GetDisplayAvatarUrl(size: 256),
            moderator.Id,
            reason,
            extra: $"**Total Warnings:** `{count}`");
    }

    private async Task LogModerationAsync(
        SocketGuild guild,
        string action,
        ulong targetId,
        string targetName,
        string? avatarUrl,
        ulong moderatorId,
        string? reason,
        string? extra)
    {
        var channel = ResolveLogChannel(guild, LogType.Moderation);

        if (channel is null)
            return;

        await SendAsync(channel, _builder.BuildModerationLog(
            action, targetId, targetName, avatarUrl, moderatorId, reason, extra));
    }

    // ---- Roles: member role add/remove (gateway diff + audit attribution) ----

    public async Task HandleGuildMemberUpdatedAsync(
        Cacheable<SocketGuildUser, ulong> before,
        SocketGuildUser after)
    {
        var channel = ResolveLogChannel(after.Guild, LogType.Roles);

        if (channel is null)
            return;

        // Without the cached "before" we can't diff which roles changed; skip
        // rather than guess (nickname/timeout/boost updates also fire here).
        if (!before.HasValue)
            return;

        var everyoneId = after.Guild.EveryoneRole.Id;
        var beforeIds = before.Value.Roles.Select(role => role.Id).ToHashSet();
        var afterIds = after.Roles.Select(role => role.Id).ToHashSet();

        var addedIds = after.Roles
            .Where(role => role.Id != everyoneId && !beforeIds.Contains(role.Id))
            .Select(role => role.Id)
            .ToList();
        var removedIds = before.Value.Roles
            .Where(role => role.Id != everyoneId && !afterIds.Contains(role.Id))
            .Select(role => role.Id)
            .ToList();

        if (addedIds.Count == 0 && removedIds.Count == 0)
            return;

        var (moderatorId, _) = await ResolveModeratorAsync(
            after.Guild, after.Id, ActionType.MemberRoleUpdated);

        await SendAsync(channel, _builder.BuildRoleUpdateLog(
            after.Id,
            after.DisplayName,
            after.GetDisplayAvatarUrl(size: 256),
            addedIds,
            removedIds,
            moderatorId));
    }

    // ---- Server: channel & role structure changes (gateway + audit) ----

    public async Task HandleChannelCreatedAsync(SocketChannel channel)
    {
        if (channel is not SocketGuildChannel guildChannel)
            return;

        var logChannel = ResolveLogChannel(guildChannel.Guild, LogType.Server);

        if (logChannel is null)
            return;

        var (moderatorId, _) = await ResolveModeratorAsync(
            guildChannel.Guild, guildChannel.Id, ActionType.ChannelCreated);

        await SendAsync(logChannel, _builder.BuildChannelCreatedLog(
            guildChannel.Id,
            DescribeChannelType(guildChannel),
            moderatorId));
    }

    public async Task HandleChannelDestroyedAsync(SocketChannel channel)
    {
        if (channel is not SocketGuildChannel guildChannel)
            return;

        var logChannel = ResolveLogChannel(guildChannel.Guild, LogType.Server);

        if (logChannel is null)
            return;

        var (moderatorId, _) = await ResolveModeratorAsync(
            guildChannel.Guild, guildChannel.Id, ActionType.ChannelDeleted);

        await SendAsync(logChannel, _builder.BuildChannelDeletedLog(
            guildChannel.Name,
            DescribeChannelType(guildChannel),
            moderatorId));
    }

    public async Task HandleChannelUpdatedAsync(
        SocketChannel before,
        SocketChannel after)
    {
        if (before is not SocketGuildChannel beforeChannel ||
            after is not SocketGuildChannel afterChannel)
        {
            return;
        }

        // Only name changes are logged to keep the server log low-noise
        // (permission/topic churn is skipped).
        if (string.Equals(beforeChannel.Name, afterChannel.Name, StringComparison.Ordinal))
            return;

        var logChannel = ResolveLogChannel(afterChannel.Guild, LogType.Server);

        if (logChannel is null)
            return;

        var (moderatorId, _) = await ResolveModeratorAsync(
            afterChannel.Guild, afterChannel.Id, ActionType.ChannelUpdated);

        await SendAsync(logChannel, _builder.BuildChannelRenamedLog(
            afterChannel.Id,
            beforeChannel.Name,
            afterChannel.Name,
            moderatorId));
    }

    public async Task HandleRoleCreatedAsync(SocketRole role)
    {
        var logChannel = ResolveLogChannel(role.Guild, LogType.Server);

        if (logChannel is null)
            return;

        var (moderatorId, _) = await ResolveModeratorAsync(
            role.Guild, role.Id, ActionType.RoleCreated);

        await SendAsync(logChannel, _builder.BuildRoleCreatedLog(
            role.Id,
            role.Name,
            moderatorId));
    }

    public async Task HandleRoleDeletedAsync(SocketRole role)
    {
        var logChannel = ResolveLogChannel(role.Guild, LogType.Server);

        if (logChannel is null)
            return;

        var (moderatorId, _) = await ResolveModeratorAsync(
            role.Guild, role.Id, ActionType.RoleDeleted);

        await SendAsync(logChannel, _builder.BuildRoleDeletedLog(
            role.Name,
            moderatorId));
    }

    public async Task HandleRoleUpdatedAsync(SocketRole before, SocketRole after)
    {
        // Only the rename is logged; colour/permission edits are skipped.
        if (string.Equals(before.Name, after.Name, StringComparison.Ordinal))
            return;

        var logChannel = ResolveLogChannel(after.Guild, LogType.Server);

        if (logChannel is null)
            return;

        var (moderatorId, _) = await ResolveModeratorAsync(
            after.Guild, after.Id, ActionType.RoleUpdated);

        await SendAsync(logChannel, _builder.BuildRoleRenamedLog(
            after.Id,
            before.Name,
            after.Name,
            moderatorId));
    }

    // ---- Shared helpers ----

    private SocketTextChannel? ResolveLogChannel(SocketGuild? guild, LogType type)
    {
        if (guild is null)
            return null;

        var config = _configService.GetConfig(guild.Id);

        if (!config.Enabled)
            return null;

        if (config.GetChannel(type) is not { } channelId)
            return null;

        return guild.GetTextChannel(channelId);
    }

    private static string DescribeChannelType(SocketGuildChannel channel)
    {
        return channel switch
        {
            SocketStageChannel => "Stage",
            SocketVoiceChannel => "Voice",
            SocketForumChannel => "Forum",
            SocketNewsChannel => "Announcement",
            SocketCategoryChannel => "Category",
            SocketThreadChannel => "Thread",
            SocketTextChannel => "Text",
            _ => "Channel"
        };
    }

    private static async Task SendAsync(
        SocketTextChannel channel,
        MessageComponent components)
    {
        try
        {
            await channel.SendMessageAsync(
                allowedMentions: AllowedMentions.None,
                components: components);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Logs Send Error] {DiscordFailure.Summarize(exception)}");
        }
    }

    // Bans/unbans arrive without a moderator; Discord writes the audit entry a
    // moment later, so we peek at the recent audit log to attribute the action.
    // This uniformly captures both bot-issued and manual (in-client) actions.
    private static async Task<(ulong? ModeratorId, string? Reason)> ResolveModeratorAsync(
        SocketGuild guild,
        ulong targetId,
        ActionType actionType)
    {
        try
        {
            await Task.Delay(AuditLogLookupDelay);

            if (!guild.CurrentUser.GuildPermissions.ViewAuditLog &&
                !guild.CurrentUser.GuildPermissions.Administrator)
            {
                return (null, null);
            }

            var entries = await guild
                .GetAuditLogsAsync(AuditLogLookupLimit, actionType: actionType)
                .FlattenAsync();

            var match = entries.FirstOrDefault(entry =>
                DateTimeOffset.UtcNow - entry.CreatedAt <= AuditLogMaxAge &&
                entry.Data switch
                {
                    BanAuditLogData ban => ban.Target?.Id == targetId,
                    UnbanAuditLogData unban => unban.Target?.Id == targetId,
                    MemberRoleAuditLogData memberRole => memberRole.Target?.Id == targetId,
                    ChannelCreateAuditLogData channelCreate => channelCreate.ChannelId == targetId,
                    ChannelDeleteAuditLogData channelDelete => channelDelete.ChannelId == targetId,
                    ChannelUpdateAuditLogData channelUpdate => channelUpdate.ChannelId == targetId,
                    RoleCreateAuditLogData roleCreate => roleCreate.RoleId == targetId,
                    RoleDeleteAuditLogData roleDelete => roleDelete.RoleId == targetId,
                    RoleUpdateAuditLogData roleUpdate => roleUpdate.RoleId == targetId,
                    _ => false
                });

            if (match?.User is null)
                return (null, match?.Reason);

            return (match.User.Id, match.Reason);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Logs Audit Error] {DiscordFailure.Summarize(exception)}");
            return (null, null);
        }
    }

    // Message deletions arrive without an actor. A matching audit entry (same
    // channel + author, recent) means a moderator/bot removed it; no match means
    // a self-delete, since authors deleting their own messages write no entry.
    private static async Task<ulong?> ResolveMessageDeleterAsync(
        SocketGuild guild,
        ulong channelId,
        ulong authorId)
    {
        try
        {
            await Task.Delay(AuditLogLookupDelay);

            if (!guild.CurrentUser.GuildPermissions.ViewAuditLog &&
                !guild.CurrentUser.GuildPermissions.Administrator)
            {
                return null;
            }

            var entries = await guild
                .GetAuditLogsAsync(
                    AuditLogLookupLimit,
                    actionType: ActionType.MessageDeleted)
                .FlattenAsync();

            var match = entries.FirstOrDefault(entry =>
                entry.Data is MessageDeleteAuditLogData data &&
                data.ChannelId == channelId &&
                data.Target?.Id == authorId &&
                DateTimeOffset.UtcNow - entry.CreatedAt <= AuditLogMaxAge);

            return match?.User?.Id;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Logs Audit Error] {DiscordFailure.Summarize(exception)}");
            return null;
        }
    }
}
