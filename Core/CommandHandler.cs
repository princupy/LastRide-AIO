using System.Reflection;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Configuration;
using LastRide.Services;

namespace LastRide.Core;

public sealed class CommandHandler
{
    private static readonly TimeSpan AuditLogLookupDelay =
        TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan AuditLogMaxAge =
        TimeSpan.FromSeconds(15);
    private const int AuditLogLookupLimit = 5;

    private readonly DiscordSocketClient _client;
    private readonly CommandService _commands;
    private readonly IServiceProvider _services;
    private readonly BotOptions _options;
    private readonly BotStatsService _statsService;
    private readonly BotOwnerService _ownerService;
    private readonly StatsComponentBuilder _statsComponentBuilder;
    private readonly HelpComponentBuilder _helpComponentBuilder;
    private readonly ProfileComponentBuilder _profileComponentBuilder;
    private readonly ServerInfoComponentBuilder _serverInfoComponentBuilder;
    private readonly RoleInfoComponentBuilder _roleInfoComponentBuilder;
    private readonly BanComponentBuilder _banComponentBuilder;
    private readonly BanConfirmationService _banConfirmationService;
    private readonly UnbanComponentBuilder _unbanComponentBuilder;
    private readonly UnbanConfirmationService _unbanConfirmationService;
    private readonly KickComponentBuilder _kickComponentBuilder;
    private readonly KickConfirmationService _kickConfirmationService;
    private readonly NukeComponentBuilder _nukeComponentBuilder;
    private readonly NukeConfirmationService _nukeConfirmationService;
    private readonly AddRoleComponentBuilder _addRoleComponentBuilder;
    private readonly SnipeComponentBuilder _snipeComponentBuilder;
    private readonly SnipeService _snipeService;
    private readonly AfkService _afkService;
    private readonly AfkComponentBuilder _afkComponentBuilder;
    private readonly MentionComponentBuilder _mentionComponentBuilder;
    private readonly UnhideAllComponentBuilder _unhideAllComponentBuilder;
    private readonly UnlockAllComponentBuilder _unlockAllComponentBuilder;

    public CommandHandler(
        DiscordSocketClient client,
        CommandService commands,
        IServiceProvider services,
        BotOptions options,
        BotStatsService statsService,
        BotOwnerService ownerService,
        StatsComponentBuilder statsComponentBuilder,
        HelpComponentBuilder helpComponentBuilder,
        ProfileComponentBuilder profileComponentBuilder,
        ServerInfoComponentBuilder serverInfoComponentBuilder,
        RoleInfoComponentBuilder roleInfoComponentBuilder,
        BanComponentBuilder banComponentBuilder,
        BanConfirmationService banConfirmationService,
        UnbanComponentBuilder unbanComponentBuilder,
        UnbanConfirmationService unbanConfirmationService,
        KickComponentBuilder kickComponentBuilder,
        KickConfirmationService kickConfirmationService,
        NukeComponentBuilder nukeComponentBuilder,
        NukeConfirmationService nukeConfirmationService,
        AddRoleComponentBuilder addRoleComponentBuilder,
        SnipeComponentBuilder snipeComponentBuilder,
        SnipeService snipeService,
        AfkService afkService,
        AfkComponentBuilder afkComponentBuilder,
        MentionComponentBuilder mentionComponentBuilder,
        UnhideAllComponentBuilder unhideAllComponentBuilder,
        UnlockAllComponentBuilder unlockAllComponentBuilder)
    {
        _client = client;
        _commands = commands;
        _services = services;
        _options = options;
        _statsService = statsService;
        _ownerService = ownerService;
        _statsComponentBuilder = statsComponentBuilder;
        _helpComponentBuilder = helpComponentBuilder;
        _profileComponentBuilder = profileComponentBuilder;
        _serverInfoComponentBuilder = serverInfoComponentBuilder;
        _roleInfoComponentBuilder = roleInfoComponentBuilder;
        _banComponentBuilder = banComponentBuilder;
        _banConfirmationService = banConfirmationService;
        _unbanComponentBuilder = unbanComponentBuilder;
        _unbanConfirmationService = unbanConfirmationService;
        _kickComponentBuilder = kickComponentBuilder;
        _kickConfirmationService = kickConfirmationService;
        _nukeComponentBuilder = nukeComponentBuilder;
        _nukeConfirmationService = nukeConfirmationService;
        _addRoleComponentBuilder = addRoleComponentBuilder;
        _snipeComponentBuilder = snipeComponentBuilder;
        _snipeService = snipeService;
        _afkService = afkService;
        _afkComponentBuilder = afkComponentBuilder;
        _mentionComponentBuilder = mentionComponentBuilder;
        _unhideAllComponentBuilder = unhideAllComponentBuilder;
        _unlockAllComponentBuilder = unlockAllComponentBuilder;
    }

    public async Task InitializeAsync()
    {
        await _commands.AddModulesAsync(
            Assembly.GetExecutingAssembly(),
            _services);

        _client.MessageReceived += HandleMessageAsync;
        _client.MessageDeleted += HandleMessageDeletedAsync;
        _client.ButtonExecuted += HandleButtonAsync;
        _client.SelectMenuExecuted += HandleSelectMenuAsync;
    }

    private Task HandleMessageDeletedAsync(
        Cacheable<IMessage, ulong> cacheable,
        Cacheable<IMessageChannel, ulong> channelCacheable)
    {
        if (!cacheable.HasValue)
            return Task.CompletedTask;

        if (cacheable.Value is not IUserMessage message)
            return Task.CompletedTask;

        if (message.Author.IsBot || message.Author.IsWebhook)
            return Task.CompletedTask;

        var authorName = message.Author is IGuildUser guildUser
            ? guildUser.DisplayName
            : message.Author.Username;

        _snipeService.Store(new LastRide.Models.SnipedMessage(
            message.Id,
            message.Channel.Id,
            message.Author.Id,
            authorName,
            message.Author.GetDisplayAvatarUrl(size: 256),
            message.Content,
            message.Attachments.Count,
            message.Author.Id,
            DateTimeOffset.UtcNow));

        if (message.Channel is SocketGuildChannel guildChannel)
        {
            _ = ResolveDeleterAsync(
                guildChannel.Guild,
                guildChannel.Id,
                message.Id,
                message.Author.Id);
        }

        return Task.CompletedTask;
    }

    private async Task ResolveDeleterAsync(
        SocketGuild guild,
        ulong channelId,
        ulong messageId,
        ulong authorId)
    {
        try
        {
            // Discord writes the audit log entry slightly after the gateway event.
            await Task.Delay(AuditLogLookupDelay);

            if (!guild.CurrentUser.GuildPermissions.ViewAuditLog &&
                !guild.CurrentUser.GuildPermissions.Administrator)
            {
                return;
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

            if (match?.User is null)
                return;

            _snipeService.SetDeletedBy(
                channelId,
                messageId,
                match.User.Id);
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"[Snipe Audit Log Error] {exception.Message}");
        }
    }

    private async Task HandleMessageAsync(SocketMessage socketMessage)
    {
        if (socketMessage is not SocketUserMessage message)
            return;

        if (message.Author.IsBot || message.Author.IsWebhook)
            return;

        var argumentPosition = 0;
        var hasPrefix = message.HasStringPrefix(
            _options.Prefix,
            ref argumentPosition);
        var commandName = hasPrefix
            ? GetCommandName(message.Content, argumentPosition)
            : string.Empty;

        if (!IsAfkCommand(commandName))
        {
            await ClearAfkIfNeededAsync(message);
        }

        await NotifyMentionedAfkUsersAsync(message);

        if (!hasPrefix && MentionsCurrentBot(message))
        {
            await SendMentionCardAsync(message);
            return;
        }

        if (!hasPrefix)
            return;

        var context = new SocketCommandContext(_client, message);

        var result = await _commands.ExecuteAsync(
            context,
            argumentPosition,
            _services);

        if (result.IsSuccess ||
            result.Error is CommandError.UnknownCommand)
        {
            return;
        }

        Console.WriteLine(
            $"[Command Error] {result.Error}: {result.ErrorReason}");
    }

    private async Task ClearAfkIfNeededAsync(SocketUserMessage message)
    {
        if (!_afkService.TryClearAfk(
                message.Author.Id,
                out var status))
        {
            return;
        }

        await message.Channel.SendMessageAsync(
            allowedMentions: AllowedMentions.None,
            components: _afkComponentBuilder.BuildWelcomeBack(status));
    }

    private async Task NotifyMentionedAfkUsersAsync(SocketUserMessage message)
    {
        var statuses = message
            .MentionedUsers
            .Where(user => user.Id != message.Author.Id)
            .Select(user => _afkService.TryGetAfk(user.Id, out var status)
                ? status
                : null)
            .Where(status => status is not null)
            .Cast<LastRide.Models.AfkStatus>()
            .DistinctBy(status => status.UserId)
            .Take(5)
            .ToArray();

        if (statuses.Length == 0)
            return;

        await message.Channel.SendMessageAsync(
            allowedMentions: AllowedMentions.None,
            components: _afkComponentBuilder.BuildMentionNotice(statuses));
    }

    private static string GetCommandName(
        string content,
        int argumentPosition)
    {
        var commandText = content[argumentPosition..].TrimStart();

        if (commandText.Length == 0)
            return string.Empty;

        var spaceIndex = commandText.IndexOf(' ');

        return spaceIndex < 0
            ? commandText
            : commandText[..spaceIndex];
    }

    private static bool IsAfkCommand(string commandName)
    {
        return commandName.Equals("afk", StringComparison.OrdinalIgnoreCase) ||
            commandName.Equals("away", StringComparison.OrdinalIgnoreCase);
    }

    private bool MentionsCurrentBot(SocketUserMessage message)
    {
        return _client.CurrentUser is not null &&
            message.MentionedUsers.Any(
                user => user.Id == _client.CurrentUser.Id);
    }

    private Task SendMentionCardAsync(SocketUserMessage message)
    {
        var botName = _client.CurrentUser.Username;
        var botAvatarUrl = _client.CurrentUser.GetDisplayAvatarUrl(size: 256);
        var commandCount = _commands.Commands.Count();

        return message.Channel.SendMessageAsync(
            allowedMentions: AllowedMentions.None,
            components: _mentionComponentBuilder.Build(
                botName,
                botAvatarUrl,
                _options.Prefix,
                commandCount));
    }

    private async Task HandleButtonAsync(SocketMessageComponent component)
    {
        if (ProfileComponentIds.TryParse(
                component.Data.CustomId,
                out var avatarView,
                out var avatarRequesterId,
                out var targetUserId,
                out var guildId))
        {
            await HandleAvatarButtonAsync(
                component,
                avatarView,
                avatarRequesterId,
                targetUserId,
                guildId);
            return;
        }

        if (ServerInfoComponentIds.TryParse(
                component.Data.CustomId,
                out var serverInfoPage,
                out var serverInfoRequesterId,
                out var serverInfoGuildId))
        {
            await HandleServerInfoButtonAsync(
                component,
                serverInfoPage,
                serverInfoRequesterId,
                serverInfoGuildId);
            return;
        }

        if (RoleInfoComponentIds.TryParse(
                component.Data.CustomId,
                out var roleInfoPage,
                out var roleInfoRequesterId,
                out var roleInfoGuildId,
                out var roleInfoRoleId))
        {
            await HandleRoleInfoButtonAsync(
                component,
                roleInfoPage,
                roleInfoRequesterId,
                roleInfoGuildId,
                roleInfoRoleId);
            return;
        }

        if (BanComponentIds.TryParse(
                component.Data.CustomId,
                out var banAction,
                out var banRequestId))
        {
            await HandleBanButtonAsync(
                component,
                banAction,
                banRequestId);
            return;
        }

        if (UnbanComponentIds.TryParse(
                component.Data.CustomId,
                out var unbanAction,
                out var unbanRequestId))
        {
            await HandleUnbanButtonAsync(
                component,
                unbanAction,
                unbanRequestId);
            return;
        }

        if (KickComponentIds.TryParse(
                component.Data.CustomId,
                out var kickAction,
                out var kickRequestId))
        {
            await HandleKickButtonAsync(
                component,
                kickAction,
                kickRequestId);
            return;
        }

        if (NukeComponentIds.TryParse(
                component.Data.CustomId,
                out var nukeAction,
                out var nukeRequestId))
        {
            await HandleNukeButtonAsync(
                component,
                nukeAction,
                nukeRequestId);
            return;
        }

        if (AddRoleComponentIds.TryParseRemove(
                component.Data.CustomId,
                out var addRoleRequesterId,
                out var addRoleGuildId,
                out var addRoleTargetId,
                out var addRoleRoleId))
        {
            await HandleAddRoleRemoveButtonAsync(
                component,
                addRoleRequesterId,
                addRoleGuildId,
                addRoleTargetId,
                addRoleRoleId);
            return;
        }

        if (SnipeComponentIds.TryParse(
                component.Data.CustomId,
                out _,
                out var snipeRequesterId,
                out var snipeChannelId,
                out var snipeIndex))
        {
            await HandleSnipeButtonAsync(
                component,
                snipeRequesterId,
                snipeChannelId,
                snipeIndex);
            return;
        }

        if (!StatsComponentIds.TryParse(
                component.Data.CustomId,
                out var tab,
                out var userId))
        {
            return;
        }

        if (component.User.Id != userId)
        {
            await component.RespondAsync(
                "Only the user who opened this stats panel can control it.",
                ephemeral: true);
            return;
        }

        try
        {
            var stats = await _statsService.CaptureAsync();
            var components = tab switch
            {
                StatsPanelTab.Developer =>
                    _statsComponentBuilder.BuildDeveloper(stats, userId),
                StatsPanelTab.Team =>
                    _statsComponentBuilder.BuildTeam(
                        await _ownerService.GetOwnerAsync(),
                        userId),
                _ => _statsComponentBuilder.BuildGeneral(stats, userId)
            };

            await component.UpdateAsync(
                properties => properties.Components = components);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Button Error] {exception}");

            if (!component.HasResponded)
            {
                await component.RespondAsync(
                    "Stats panel update failed.",
                    ephemeral: true);
            }
        }
    }

    private async Task HandleUnbanButtonAsync(
        SocketMessageComponent component,
        UnbanAction action,
        string requestId)
    {
        if (!_unbanConfirmationService.TryGet(
                requestId,
                out var request))
        {
            await component.RespondAsync(
                "This unban request has expired.",
                ephemeral: true);
            return;
        }

        if (component.User.Id != request.RequesterId)
        {
            await component.RespondAsync(
                "Only the user who opened this unban request can control it.",
                ephemeral: true);
            return;
        }

        if (action == UnbanAction.Cancel)
        {
            _unbanConfirmationService.TryRemove(requestId, out _);

            await component.UpdateAsync(properties =>
            {
                properties.AllowedMentions = AllowedMentions.None;
                properties.Components = _unbanComponentBuilder.BuildCancelled(
                    request);
            });
            return;
        }

        await component.DeferAsync();

        try
        {
            var guild = _client.GetGuild(request.GuildId);
            var moderator = guild?.GetUser(request.RequesterId);

            if (guild is null || moderator is null)
            {
                await component.FollowupAsync(
                    "I could not fetch the server or moderator.",
                    ephemeral: true);
                return;
            }

            var validationError = ValidateUnbanRequest(
                guild,
                moderator);

            if (validationError is not null)
            {
                await component.FollowupAsync(
                    validationError,
                    ephemeral: true);
                return;
            }

            if (!await IsUserBannedAsync(guild, request.TargetId))
            {
                await component.FollowupAsync(
                    "That user is no longer banned.",
                    ephemeral: true);
                return;
            }

            await guild.RemoveBanAsync(request.TargetId);

            _unbanConfirmationService.TryRemove(requestId, out _);

            await component.ModifyOriginalResponseAsync(properties =>
            {
                properties.AllowedMentions = AllowedMentions.None;
                properties.Components = _unbanComponentBuilder.BuildSuccess(
                    request);
            });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Unban Button Error] {exception}");

            await component.FollowupAsync(
                "Unban failed. Check my permissions.",
                ephemeral: true);
        }
    }

    private static string? ValidateUnbanRequest(
        SocketGuild guild,
        SocketGuildUser moderator)
    {
        if (!HasBanPermission(moderator.GuildPermissions))
            return "You no longer have permission to unban members.";

        if (!HasBanPermission(guild.CurrentUser.GuildPermissions))
            return "I no longer have permission to unban members.";

        return null;
    }

    private static async Task<bool> IsUserBannedAsync(
        SocketGuild guild,
        ulong userId)
    {
        try
        {
            return await guild.GetBanAsync(userId) is not null;
        }
        catch
        {
            return false;
        }
    }

    private async Task HandleBanButtonAsync(
        SocketMessageComponent component,
        BanAction action,
        string requestId)
    {
        if (!_banConfirmationService.TryGet(
                requestId,
                out var request))
        {
            await component.RespondAsync(
                "This ban request has expired.",
                ephemeral: true);
            return;
        }

        if (component.User.Id != request.RequesterId)
        {
            await component.RespondAsync(
                "Only the user who opened this ban request can control it.",
                ephemeral: true);
            return;
        }

        if (action == BanAction.Cancel)
        {
            _banConfirmationService.TryRemove(requestId, out _);

            await component.UpdateAsync(properties =>
            {
                properties.AllowedMentions = AllowedMentions.None;
                properties.Components = _banComponentBuilder.BuildCancelled(
                    request);
            });
            return;
        }

        await component.DeferAsync();

        try
        {
            var guild = _client.GetGuild(request.GuildId);
            var moderator = guild?.GetUser(request.RequesterId);

            if (guild is null || moderator is null)
            {
                await component.FollowupAsync(
                    "I could not fetch the server or moderator.",
                    ephemeral: true);
                return;
            }

            var validationError = ValidateBanRequest(
                guild,
                moderator,
                request.TargetId);

            if (validationError is not null)
            {
                await component.FollowupAsync(
                    validationError,
                    ephemeral: true);
                return;
            }

            await guild.AddBanAsync(
                request.TargetId,
                0,
                $"Banned by {component.User.Username}: {request.Reason}");

            _banConfirmationService.TryRemove(requestId, out _);

            await component.ModifyOriginalResponseAsync(properties =>
            {
                properties.AllowedMentions = AllowedMentions.None;
                properties.Components = _banComponentBuilder.BuildSuccess(
                    request);
            });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Ban Button Error] {exception}");

            await component.FollowupAsync(
                "Ban failed. Check my permissions and role position.",
                ephemeral: true);
        }
    }

    private static string? ValidateBanRequest(
        SocketGuild guild,
        SocketGuildUser moderator,
        ulong targetId)
    {
        if (!HasBanPermission(moderator.GuildPermissions))
            return "You no longer have permission to ban members.";

        if (!HasBanPermission(guild.CurrentUser.GuildPermissions))
            return "I no longer have permission to ban members.";

        if (targetId == moderator.Id)
            return "You cannot ban yourself.";

        if (targetId == guild.CurrentUser.Id)
            return "I cannot ban myself.";

        if (targetId == guild.OwnerId)
            return "The server owner cannot be banned.";

        var targetMember = guild.GetUser(targetId);

        if (targetMember is null)
            return null;

        if (moderator.Id != guild.OwnerId &&
            targetMember.Hierarchy >= moderator.Hierarchy)
        {
            return "You cannot ban a member with an equal or higher role.";
        }

        if (targetMember.Hierarchy >= guild.CurrentUser.Hierarchy)
            return "My highest role must be above the target member's highest role.";

        return null;
    }

    private static bool HasBanPermission(GuildPermissions permissions)
    {
        return permissions.BanMembers || permissions.Administrator;
    }

    private async Task HandleKickButtonAsync(
        SocketMessageComponent component,
        KickAction action,
        string requestId)
    {
        if (!_kickConfirmationService.TryGet(
                requestId,
                out var request))
        {
            await component.RespondAsync(
                "This kick request has expired.",
                ephemeral: true);
            return;
        }

        if (component.User.Id != request.RequesterId)
        {
            await component.RespondAsync(
                "Only the user who opened this kick request can control it.",
                ephemeral: true);
            return;
        }

        if (action == KickAction.Cancel)
        {
            _kickConfirmationService.TryRemove(requestId, out _);

            await component.UpdateAsync(properties =>
            {
                properties.AllowedMentions = AllowedMentions.None;
                properties.Components = _kickComponentBuilder.BuildCancelled(
                    request);
            });
            return;
        }

        await component.DeferAsync();

        try
        {
            var guild = _client.GetGuild(request.GuildId);
            var moderator = guild?.GetUser(request.RequesterId);

            if (guild is null || moderator is null)
            {
                await component.FollowupAsync(
                    "I could not fetch the server or moderator.",
                    ephemeral: true);
                return;
            }

            var validationError = ValidateKickRequest(
                guild,
                moderator,
                request.TargetId);

            if (validationError is not null)
            {
                await component.FollowupAsync(
                    validationError,
                    ephemeral: true);
                return;
            }

            var targetMember = guild.GetUser(request.TargetId);

            if (targetMember is null)
            {
                await component.FollowupAsync(
                    "That user is no longer in this server.",
                    ephemeral: true);
                return;
            }

            await targetMember.KickAsync(
                $"Kicked by {component.User.Username}: {request.Reason}");

            _kickConfirmationService.TryRemove(requestId, out _);

            await component.ModifyOriginalResponseAsync(properties =>
            {
                properties.AllowedMentions = AllowedMentions.None;
                properties.Components = _kickComponentBuilder.BuildSuccess(
                    request);
            });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Kick Button Error] {exception}");

            await component.FollowupAsync(
                "Kick failed. Check my permissions and role position.",
                ephemeral: true);
        }
    }

    private static string? ValidateKickRequest(
        SocketGuild guild,
        SocketGuildUser moderator,
        ulong targetId)
    {
        if (!HasKickPermission(moderator.GuildPermissions))
            return "You no longer have permission to kick members.";

        if (!HasKickPermission(guild.CurrentUser.GuildPermissions))
            return "I no longer have permission to kick members.";

        if (targetId == moderator.Id)
            return "You cannot kick yourself.";

        if (targetId == guild.CurrentUser.Id)
            return "I cannot kick myself.";

        if (targetId == guild.OwnerId)
            return "The server owner cannot be kicked.";

        var targetMember = guild.GetUser(targetId);

        if (targetMember is null)
            return "That user is no longer in this server.";

        if (moderator.Id != guild.OwnerId &&
            targetMember.Hierarchy >= moderator.Hierarchy)
        {
            return "You cannot kick a member with an equal or higher role.";
        }

        if (targetMember.Hierarchy >= guild.CurrentUser.Hierarchy)
            return "My highest role must be above the target member's highest role.";

        return null;
    }

    private static bool HasKickPermission(GuildPermissions permissions)
    {
        return permissions.KickMembers || permissions.Administrator;
    }

    private async Task HandleNukeButtonAsync(
        SocketMessageComponent component,
        NukeAction action,
        string requestId)
    {
        if (!_nukeConfirmationService.TryGet(
                requestId,
                out var request))
        {
            await component.RespondAsync(
                "This nuke request has expired.",
                ephemeral: true);
            return;
        }

        if (component.User.Id != request.RequesterId)
        {
            await component.RespondAsync(
                "Only the user who opened this nuke request can control it.",
                ephemeral: true);
            return;
        }

        if (action == NukeAction.Cancel)
        {
            _nukeConfirmationService.TryRemove(requestId, out _);

            await component.UpdateAsync(properties =>
            {
                properties.AllowedMentions = AllowedMentions.None;
                properties.Components = _nukeComponentBuilder.BuildCancelled(
                    request);
            });
            return;
        }

        await component.DeferAsync();

        try
        {
            var guild = _client.GetGuild(request.GuildId);
            var moderator = guild?.GetUser(request.RequesterId);

            if (guild is null || moderator is null)
            {
                await component.FollowupAsync(
                    "I could not fetch the server or moderator.",
                    ephemeral: true);
                return;
            }

            if (!moderator.GuildPermissions.Administrator)
            {
                await component.FollowupAsync(
                    "You no longer have Administrator permission.",
                    ephemeral: true);
                return;
            }

            if (!guild.CurrentUser.GuildPermissions.ManageChannels &&
                !guild.CurrentUser.GuildPermissions.Administrator)
            {
                await component.FollowupAsync(
                    "I no longer have permission to manage channels.",
                    ephemeral: true);
                return;
            }

            if (guild.GetChannel(request.ChannelId) is not SocketTextChannel channel)
            {
                await component.FollowupAsync(
                    "That channel no longer exists.",
                    ephemeral: true);
                return;
            }

            var newChannel = await CloneTextChannelAsync(guild, channel, moderator.Username);

            await channel.DeleteAsync(new RequestOptions
            {
                AuditLogReason = $"Nuked by {moderator.Username}"
            });

            _nukeConfirmationService.TryRemove(requestId, out _);

            await newChannel.SendMessageAsync(
                allowedMentions: AllowedMentions.None,
                components: _nukeComponentBuilder.BuildSuccess(moderator.Id));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Nuke Button Error] {exception}");

            await component.FollowupAsync(
                "Nuke failed. Check my permissions and role position.",
                ephemeral: true);
        }
    }

    private static async Task<ITextChannel> CloneTextChannelAsync(
        SocketGuild guild,
        SocketTextChannel source,
        string moderatorName)
    {
        var newChannel = await guild.CreateTextChannelAsync(
            source.Name,
            properties =>
            {
                properties.CategoryId = source.CategoryId;
                properties.Position = source.Position;
                properties.Topic = source.Topic;
                properties.SlowModeInterval = source.SlowModeInterval;
                properties.IsNsfw = source.IsNsfw;
                properties.PermissionOverwrites = source.PermissionOverwrites.ToArray();
            },
            new RequestOptions
            {
                AuditLogReason = $"Nuked by {moderatorName}"
            });

        return newChannel;
    }

    private async Task HandleRoleInfoButtonAsync(
        SocketMessageComponent component,
        RoleInfoPage page,
        ulong requesterId,
        ulong guildId,
        ulong roleId)
    {
        if (component.User.Id != requesterId)
        {
            await component.RespondAsync(
                "Only the user who opened this role info panel can control it.",
                ephemeral: true);
            return;
        }

        try
        {
            var guild = _client.GetGuild(guildId);
            var role = guild?.GetRole(roleId);

            if (guild is null || role is null)
            {
                await component.RespondAsync(
                    "I could not fetch that role.",
                    ephemeral: true);
                return;
            }

            await component.UpdateAsync(properties =>
            {
                properties.AllowedMentions = AllowedMentions.None;
                properties.Components = _roleInfoComponentBuilder.Build(
                    role,
                    requesterId,
                    page);
            });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Role Info Button Error] {exception}");

            if (!component.HasResponded)
            {
                await component.RespondAsync(
                    "Role info panel update failed.",
                    ephemeral: true);
            }
        }
    }

    private async Task HandleServerInfoButtonAsync(
        SocketMessageComponent component,
        ServerInfoPage page,
        ulong requesterId,
        ulong guildId)
    {
        if (component.User.Id != requesterId)
        {
            await component.RespondAsync(
                "Only the user who opened this server info panel can control it.",
                ephemeral: true);
            return;
        }

        try
        {
            var guild = _client.GetGuild(guildId);

            if (guild is null)
            {
                await component.RespondAsync(
                    "I could not fetch that server.",
                    ephemeral: true);
                return;
            }

            await component.UpdateAsync(
                properties => properties.Components =
                    _serverInfoComponentBuilder.Build(
                        guild,
                        requesterId,
                        page));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Server Info Button Error] {exception}");

            if (!component.HasResponded)
            {
                await component.RespondAsync(
                    "Server info panel update failed.",
                    ephemeral: true);
            }
        }
    }

    private async Task HandleAvatarButtonAsync(
        SocketMessageComponent component,
        AvatarView avatarView,
        ulong requesterId,
        ulong targetUserId,
        ulong guildId)
    {
        if (component.User.Id != requesterId)
        {
            await component.RespondAsync(
                "Only the user who opened this avatar panel can control it.",
                ephemeral: true);
            return;
        }

        try
        {
            var guildUser = guildId == 0
                ? null
                : _client.GetGuild(guildId)?.GetUser(targetUserId);

            var targetUser =
                guildUser as SocketUser ??
                _client.GetUser(targetUserId) ??
                await _client.GetUserAsync(targetUserId);

            if (targetUser is null)
            {
                await component.RespondAsync(
                    "I could not fetch that user.",
                    ephemeral: true);
                return;
            }

            var components = _profileComponentBuilder.BuildAvatar(
                targetUser,
                guildUser,
                requesterId,
                guildId,
                avatarView);

            await component.UpdateAsync(
                properties => properties.Components = components);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Avatar Button Error] {exception}");

            if (!component.HasResponded)
            {
                await component.RespondAsync(
                    "Avatar panel update failed.",
                    ephemeral: true);
            }
        }
    }

    private async Task HandleSelectMenuAsync(SocketMessageComponent component)
    {
        if (UnhideAllComponentIds.TryParseMenu(
                component.Data.CustomId,
                out var unhideAllRequesterId,
                out var unhideAllGuildId))
        {
            await HandleUnhideAllSelectAsync(
                component,
                unhideAllRequesterId,
                unhideAllGuildId);
            return;
        }

        if (UnlockAllComponentIds.TryParseMenu(
                component.Data.CustomId,
                out var unlockAllRequesterId,
                out var unlockAllGuildId))
        {
            await HandleUnlockAllSelectAsync(
                component,
                unlockAllRequesterId,
                unlockAllGuildId);
            return;
        }

        if (!HelpComponentIds.TryParse(
                component.Data.CustomId,
                out var userId))
        {
            return;
        }

        if (component.User.Id != userId)
        {
            await component.RespondAsync(
                "Only the user who opened this help menu can control it.",
                ephemeral: true);
            return;
        }

        var selectedValue = component.Data.Values.FirstOrDefault();

        if (!HelpComponentIds.TryParseCategory(
                selectedValue,
                out var category))
        {
            await component.RespondAsync(
                "That help category is not available.",
                ephemeral: true);
            return;
        }

        try
        {
            var components = _helpComponentBuilder.Build(
                userId,
                _options.Prefix,
                component.User.Mention,
                _client.CurrentUser.Username,
                _client.CurrentUser.GetDisplayAvatarUrl(size: 256),
                category);

            await component.UpdateAsync(
                properties => properties.Components = components);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Select Menu Error] {exception}");

            if (!component.HasResponded)
            {
                await component.RespondAsync(
                    "Help menu update failed.",
                    ephemeral: true);
            }
        }
    }

    private async Task HandleUnhideAllSelectAsync(
        SocketMessageComponent component,
        ulong requesterId,
        ulong guildId)
    {
        if (component.User.Id != requesterId)
        {
            await component.RespondAsync(
                "Only the user who opened this unhide menu can control it.",
                ephemeral: true);
            return;
        }

        await component.DeferAsync();

        try
        {
            var guild = _client.GetGuild(guildId);
            var moderator = guild?.GetUser(requesterId);

            if (guild is null || moderator is null)
            {
                await component.FollowupAsync(
                    "I could not fetch the server or moderator.",
                    ephemeral: true);
                return;
            }

            if (!HasManageChannels(moderator.GuildPermissions))
            {
                await component.FollowupAsync(
                    "You no longer have permission to manage channels.",
                    ephemeral: true);
                return;
            }

            if (!CanEditChannelPermissions(guild.CurrentUser.GuildPermissions))
            {
                await component.FollowupAsync(
                    "I no longer have permission to edit channel permissions.",
                    ephemeral: true);
                return;
            }

            var selectedValues = component.Data.Values.ToArray();
            var targets = selectedValues.Contains(
                    UnhideAllComponentIds.AllChannelsValue,
                    StringComparer.Ordinal)
                ? UnhideAllComponentBuilder.GetHiddenChannels(guild)
                : GetSelectedHiddenChannels(guild, selectedValues);

            if (targets.Length == 0)
            {
                await component.ModifyOriginalResponseAsync(properties =>
                {
                    properties.AllowedMentions = AllowedMentions.None;
                    properties.Components = _unhideAllComponentBuilder.BuildNotice(
                        "No Hidden Channels",
                        "The selected channels are no longer hidden.");
                });
                return;
            }

            var everyoneRole = guild.EveryoneRole;
            var unhiddenCount = 0;
            var skippedCount = 0;
            var failedCount = 0;

            foreach (var channel in targets)
            {
                var currentOverwrite = channel.GetPermissionOverwrite(
                    everyoneRole);

                if (currentOverwrite?.ViewChannel != PermValue.Deny)
                {
                    skippedCount++;
                    continue;
                }

                try
                {
                    var overwrite =
                        currentOverwrite ?? OverwritePermissions.InheritAll;

                    await channel.AddPermissionOverwriteAsync(
                        everyoneRole,
                        overwrite.Modify(viewChannel: PermValue.Inherit),
                        new RequestOptions
                        {
                            AuditLogReason =
                                $"Unhide all by {moderator.Username}"
                        });

                    unhiddenCount++;
                }
                catch (Exception exception)
                {
                    failedCount++;
                    Console.WriteLine(
                        $"[UnhideAll Error] #{channel.Name} ({channel.Id}): {exception.Message}");
                }
            }

            await component.ModifyOriginalResponseAsync(properties =>
            {
                properties.AllowedMentions = AllowedMentions.None;
                properties.Components = _unhideAllComponentBuilder.BuildSuccess(
                    unhiddenCount,
                    skippedCount,
                    failedCount,
                    moderator.Id);
            });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[UnhideAll Select Error] {exception}");

            await component.FollowupAsync(
                "Unhide all failed. Check my permissions and role position.",
                ephemeral: true);
        }
    }

    private static SocketGuildChannel[] GetSelectedHiddenChannels(
        SocketGuild guild,
        IEnumerable<string> selectedValues)
    {
        var everyoneRole = guild.EveryoneRole;
        var channelIds = selectedValues
            .Select(value =>
                UnhideAllComponentIds.TryParseChannelValue(
                    value,
                    out var channelId)
                    ? channelId
                    : 0)
            .Where(channelId => channelId != 0)
            .Distinct()
            .ToArray();

        return guild.Channels
            .Where(channel =>
                channelIds.Contains(channel.Id) &&
                channel.GetPermissionOverwrite(everyoneRole)?.ViewChannel ==
                PermValue.Deny)
            .OrderBy(channel => channel.Position)
            .ThenBy(channel => channel.Name)
            .ToArray();
    }

    private async Task HandleUnlockAllSelectAsync(
        SocketMessageComponent component,
        ulong requesterId,
        ulong guildId)
    {
        if (component.User.Id != requesterId)
        {
            await component.RespondAsync(
                "Only the user who opened this unlock menu can control it.",
                ephemeral: true);
            return;
        }

        await component.DeferAsync();

        try
        {
            var guild = _client.GetGuild(guildId);
            var moderator = guild?.GetUser(requesterId);

            if (guild is null || moderator is null)
            {
                await component.FollowupAsync(
                    "I could not fetch the server or moderator.",
                    ephemeral: true);
                return;
            }

            if (!HasManageChannels(moderator.GuildPermissions))
            {
                await component.FollowupAsync(
                    "You no longer have permission to manage channels.",
                    ephemeral: true);
                return;
            }

            if (!CanEditChannelPermissions(guild.CurrentUser.GuildPermissions))
            {
                await component.FollowupAsync(
                    "I no longer have permission to edit channel permissions.",
                    ephemeral: true);
                return;
            }

            var selectedValues = component.Data.Values.ToArray();
            var targets = selectedValues.Contains(
                    UnlockAllComponentIds.AllChannelsValue,
                    StringComparer.Ordinal)
                ? UnlockAllComponentBuilder.GetLockedChannels(guild)
                : GetSelectedLockedChannels(guild, selectedValues);

            if (targets.Length == 0)
            {
                await component.ModifyOriginalResponseAsync(properties =>
                {
                    properties.AllowedMentions = AllowedMentions.None;
                    properties.Components = _unlockAllComponentBuilder.BuildNotice(
                        "No Locked Channels",
                        "The selected channels are no longer locked.");
                });
                return;
            }

            var everyoneRole = guild.EveryoneRole;
            var unlockedCount = 0;
            var skippedCount = 0;
            var failedCount = 0;

            foreach (var channel in targets)
            {
                var currentOverwrite = channel.GetPermissionOverwrite(
                    everyoneRole);

                if (currentOverwrite?.SendMessages != PermValue.Deny)
                {
                    skippedCount++;
                    continue;
                }

                try
                {
                    var overwrite =
                        currentOverwrite ?? OverwritePermissions.InheritAll;

                    await channel.AddPermissionOverwriteAsync(
                        everyoneRole,
                        overwrite.Modify(sendMessages: PermValue.Inherit),
                        new RequestOptions
                        {
                            AuditLogReason =
                                $"Unlock all by {moderator.Username}"
                        });

                    unlockedCount++;
                }
                catch (Exception exception)
                {
                    failedCount++;
                    Console.WriteLine(
                        $"[UnlockAll Error] #{channel.Name} ({channel.Id}): {exception.Message}");
                }
            }

            await component.ModifyOriginalResponseAsync(properties =>
            {
                properties.AllowedMentions = AllowedMentions.None;
                properties.Components = _unlockAllComponentBuilder.BuildSuccess(
                    unlockedCount,
                    skippedCount,
                    failedCount,
                    moderator.Id);
            });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[UnlockAll Select Error] {exception}");

            await component.FollowupAsync(
                "Unlock all failed. Check my permissions and role position.",
                ephemeral: true);
        }
    }

    private static SocketGuildChannel[] GetSelectedLockedChannels(
        SocketGuild guild,
        IEnumerable<string> selectedValues)
    {
        var everyoneRole = guild.EveryoneRole;
        var channelIds = selectedValues
            .Select(value =>
                UnlockAllComponentIds.TryParseChannelValue(
                    value,
                    out var channelId)
                    ? channelId
                    : 0)
            .Where(channelId => channelId != 0)
            .Distinct()
            .ToArray();

        return guild.Channels
            .Where(channel =>
                channelIds.Contains(channel.Id) &&
                channel.GetPermissionOverwrite(everyoneRole)?.SendMessages ==
                PermValue.Deny)
            .OrderBy(channel => channel.Position)
            .ThenBy(channel => channel.Name)
            .ToArray();
    }

    private async Task HandleSnipeButtonAsync(
        SocketMessageComponent component,
        ulong requesterId,
        ulong channelId,
        int index)
    {
        if (component.User.Id != requesterId)
        {
            await component.RespondAsync(
                "Only the user who opened this snipe panel can control it.",
                ephemeral: true);
            return;
        }

        try
        {
            var messages = _snipeService.GetMessages(channelId);

            await component.UpdateAsync(properties =>
            {
                properties.AllowedMentions = AllowedMentions.None;
                properties.Components = _snipeComponentBuilder.Build(
                    messages,
                    requesterId,
                    channelId,
                    index);
            });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Snipe Button Error] {exception}");

            if (!component.HasResponded)
            {
                await component.RespondAsync(
                    "Snipe panel update failed.",
                    ephemeral: true);
            }
        }
    }

    private async Task HandleAddRoleRemoveButtonAsync(
        SocketMessageComponent component,
        ulong requesterId,
        ulong guildId,
        ulong targetId,
        ulong roleId)
    {
        if (component.User.Id != requesterId)
        {
            await component.RespondAsync(
                "Only the user who ran this command can remove the role.",
                ephemeral: true);
            return;
        }

        await component.DeferAsync();

        try
        {
            var guild = _client.GetGuild(guildId);
            var moderator = guild?.GetUser(requesterId);

            if (guild is null || moderator is null)
            {
                await component.FollowupAsync(
                    "I could not fetch the server or moderator.",
                    ephemeral: true);
                return;
            }

            if (!HasManageRoles(moderator.GuildPermissions))
            {
                await component.FollowupAsync(
                    "You no longer have permission to manage roles.",
                    ephemeral: true);
                return;
            }

            if (!HasManageRoles(guild.CurrentUser.GuildPermissions))
            {
                await component.FollowupAsync(
                    "I no longer have permission to manage roles.",
                    ephemeral: true);
                return;
            }

            var target = guild.GetUser(targetId);
            var role = guild.GetRole(roleId);

            if (target is null || role is null)
            {
                await component.FollowupAsync(
                    "I could not fetch that member or role.",
                    ephemeral: true);
                return;
            }

            var validationError = ValidateRoleRemoval(
                guild,
                moderator,
                role);

            if (validationError is not null)
            {
                await component.FollowupAsync(
                    validationError,
                    ephemeral: true);
                return;
            }

            if (target.Roles.All(existing => existing.Id != role.Id))
            {
                await component.ModifyOriginalResponseAsync(properties =>
                {
                    properties.AllowedMentions = AllowedMentions.None;
                    properties.Components = _addRoleComponentBuilder.BuildNotice(
                        "Role Not Found",
                        "That member no longer has this role.");
                });
                return;
            }

            await target.RemoveRoleAsync(
                role,
                new RequestOptions
                {
                    AuditLogReason = $"Role removed by {moderator.Username}"
                });

            await component.ModifyOriginalResponseAsync(properties =>
            {
                properties.AllowedMentions = AllowedMentions.None;
                properties.Components = _addRoleComponentBuilder.BuildRemoved(
                    target.Id,
                    target.DisplayName,
                    target.GetDisplayAvatarUrl(size: 256),
                    role.Name,
                    role.Id,
                    moderator.Id);
            });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[AddRole Remove Button Error] {exception}");

            await component.FollowupAsync(
                "Role removal failed. Check my permissions and role position.",
                ephemeral: true);
        }
    }

    private static string? ValidateRoleRemoval(
        SocketGuild guild,
        SocketGuildUser moderator,
        SocketRole role)
    {
        if (role.Id == guild.EveryoneRole.Id)
            return "The `@everyone` role cannot be removed.";

        if (role.IsManaged)
            return "That role is managed by an integration and cannot be removed.";

        if (moderator.Id != guild.OwnerId &&
            role.Position >= moderator.Hierarchy)
        {
            return "You cannot remove a role that is equal to or higher than your highest role.";
        }

        if (role.Position >= guild.CurrentUser.Hierarchy)
            return "My highest role must be above the role you want to remove.";

        return null;
    }

    private static bool HasManageRoles(GuildPermissions permissions)
    {
        return permissions.ManageRoles || permissions.Administrator;
    }

    private static bool HasManageChannels(GuildPermissions permissions)
    {
        return permissions.ManageChannels || permissions.Administrator;
    }

    private static bool CanEditChannelPermissions(GuildPermissions permissions)
    {
        return permissions.ManageRoles || permissions.Administrator;
    }
}
