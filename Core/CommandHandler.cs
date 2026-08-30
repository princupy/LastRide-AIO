using System.Reflection;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Configuration;
using LastRide.Models;
using LastRide.Services;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;

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
    private readonly BanListComponentBuilder _banListComponentBuilder;
    private readonly BanListService _banListService;
    private readonly KickComponentBuilder _kickComponentBuilder;
    private readonly KickConfirmationService _kickConfirmationService;
    private readonly NukeComponentBuilder _nukeComponentBuilder;
    private readonly NukeConfirmationService _nukeConfirmationService;
    private readonly AddRoleComponentBuilder _addRoleComponentBuilder;
    private readonly SnipeComponentBuilder _snipeComponentBuilder;
    private readonly SnipeService _snipeService;
    private readonly PrefixService _prefixService;
    private readonly AutoModConfigService _autoModConfigService;
    private readonly AutoModService _autoModService;
    private readonly AutoModComponentBuilder _autoModComponentBuilder;
    private readonly AutoRoleConfigService _autoRoleConfigService;
    private readonly AutoRoleService _autoRoleService;
    private readonly AutoResponderConfigService _autoResponderConfigService;
    private readonly AutoResponderService _autoResponderService;
    private readonly AfkService _afkService;
    private readonly AfkComponentBuilder _afkComponentBuilder;
    private readonly MentionComponentBuilder _mentionComponentBuilder;
    private readonly UnhideAllComponentBuilder _unhideAllComponentBuilder;
    private readonly UnlockAllComponentBuilder _unlockAllComponentBuilder;
    private readonly VoiceComponentBuilder _voiceComponentBuilder;
    private readonly LogConfigService _logConfigService;
    private readonly LogService _logService;
    private readonly LogComponentBuilder _logComponentBuilder;
    private readonly LevelConfigService _levelConfigService;
    private readonly LevelService _levelService;
    private readonly LevelComponentBuilder _levelComponentBuilder;
    private readonly SetupRoleConfigService _setupRoleConfigService;
    private readonly SetupRoleService _setupRoleService;
    private readonly WelcomeConfigService _welcomeConfigService;
    private readonly WelcomeService _welcomeService;
    private readonly TicketConfigService _ticketConfigService;
    private readonly TicketService _ticketService;
    private readonly TicketComponentBuilder _ticketComponentBuilder;
    private readonly MediaConfigService _mediaConfigService;
    private readonly MediaService _mediaService;
    private readonly GiveawayService _giveawayService;
    private readonly GiveawayComponentBuilder _giveawayComponentBuilder;
    private readonly NoPrefixService _noPrefixService;
    private readonly NoPrefixComponentBuilder _noPrefixComponentBuilder;
    private readonly MusicService _musicService;
    private readonly MusicComponentBuilder _musicComponentBuilder;
    private readonly CommandAccessService _commandAccessService;

    public CommandHandler(
        DiscordSocketClient client,
        CommandService commands,
        IServiceProvider services,
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
        BanListComponentBuilder banListComponentBuilder,
        BanListService banListService,
        KickComponentBuilder kickComponentBuilder,
        KickConfirmationService kickConfirmationService,
        NukeComponentBuilder nukeComponentBuilder,
        NukeConfirmationService nukeConfirmationService,
        AddRoleComponentBuilder addRoleComponentBuilder,
        SnipeComponentBuilder snipeComponentBuilder,
        SnipeService snipeService,
        PrefixService prefixService,
        AutoModConfigService autoModConfigService,
        AutoModService autoModService,
        AutoModComponentBuilder autoModComponentBuilder,
        AutoRoleConfigService autoRoleConfigService,
        AutoRoleService autoRoleService,
        AutoResponderConfigService autoResponderConfigService,
        AutoResponderService autoResponderService,
        AfkService afkService,
        AfkComponentBuilder afkComponentBuilder,
        MentionComponentBuilder mentionComponentBuilder,
        UnhideAllComponentBuilder unhideAllComponentBuilder,
        UnlockAllComponentBuilder unlockAllComponentBuilder,
        VoiceComponentBuilder voiceComponentBuilder,
        LogConfigService logConfigService,
        LogService logService,
        LogComponentBuilder logComponentBuilder,
        LevelConfigService levelConfigService,
        LevelService levelService,
        LevelComponentBuilder levelComponentBuilder,
        SetupRoleConfigService setupRoleConfigService,
        SetupRoleService setupRoleService,
        WelcomeConfigService welcomeConfigService,
        WelcomeService welcomeService,
        TicketConfigService ticketConfigService,
        TicketService ticketService,
        TicketComponentBuilder ticketComponentBuilder,
        MediaConfigService mediaConfigService,
        MediaService mediaService,
        GiveawayService giveawayService,
        GiveawayComponentBuilder giveawayComponentBuilder,
        NoPrefixService noPrefixService,
        NoPrefixComponentBuilder noPrefixComponentBuilder,
        MusicService musicService,
        MusicComponentBuilder musicComponentBuilder,
        CommandAccessService commandAccessService)
    {
        _client = client;
        _commands = commands;
        _services = services;
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
        _banListComponentBuilder = banListComponentBuilder;
        _banListService = banListService;
        _kickComponentBuilder = kickComponentBuilder;
        _kickConfirmationService = kickConfirmationService;
        _nukeComponentBuilder = nukeComponentBuilder;
        _nukeConfirmationService = nukeConfirmationService;
        _addRoleComponentBuilder = addRoleComponentBuilder;
        _snipeComponentBuilder = snipeComponentBuilder;
        _snipeService = snipeService;
        _prefixService = prefixService;
        _autoModConfigService = autoModConfigService;
        _autoModService = autoModService;
        _autoModComponentBuilder = autoModComponentBuilder;
        _autoRoleConfigService = autoRoleConfigService;
        _autoRoleService = autoRoleService;
        _autoResponderConfigService = autoResponderConfigService;
        _autoResponderService = autoResponderService;
        _afkService = afkService;
        _afkComponentBuilder = afkComponentBuilder;
        _mentionComponentBuilder = mentionComponentBuilder;
        _unhideAllComponentBuilder = unhideAllComponentBuilder;
        _unlockAllComponentBuilder = unlockAllComponentBuilder;
        _voiceComponentBuilder = voiceComponentBuilder;
        _logConfigService = logConfigService;
        _logService = logService;
        _logComponentBuilder = logComponentBuilder;
        _levelConfigService = levelConfigService;
        _levelService = levelService;
        _levelComponentBuilder = levelComponentBuilder;
        _setupRoleConfigService = setupRoleConfigService;
        _setupRoleService = setupRoleService;
        _welcomeConfigService = welcomeConfigService;
        _welcomeService = welcomeService;
        _ticketConfigService = ticketConfigService;
        _ticketService = ticketService;
        _ticketComponentBuilder = ticketComponentBuilder;
        _mediaConfigService = mediaConfigService;
        _mediaService = mediaService;
        _giveawayService = giveawayService;
        _giveawayComponentBuilder = giveawayComponentBuilder;
        _noPrefixService = noPrefixService;
        _noPrefixComponentBuilder = noPrefixComponentBuilder;
        _musicService = musicService;
        _musicComponentBuilder = musicComponentBuilder;
        _commandAccessService = commandAccessService;
    }

    public async Task InitializeAsync()
    {
        await _commands.AddModulesAsync(
            Assembly.GetExecutingAssembly(),
            _services);

        await _prefixService.LoadAsync();
        await _autoModConfigService.LoadAsync();
        await _autoRoleConfigService.LoadAsync();
        await _autoResponderConfigService.LoadAsync();
        await _logConfigService.LoadAsync();
        await _levelConfigService.LoadAsync();
        await _levelService.LoadAsync();
        await _setupRoleConfigService.LoadAsync();
        await _welcomeConfigService.LoadAsync();
        await _ticketConfigService.LoadAsync();
        await _ticketService.LoadAsync();
        await _mediaConfigService.LoadAsync();
        await _giveawayService.LoadAsync();
        await _noPrefixService.LoadAsync();

        // Modules are registered by now, so the access table can be checked against
        // the real command list and report anything that was added without a rule.
        _commandAccessService.Validate();

        _client.MessageReceived += HandleMessageAsync;
        _client.MessageDeleted += HandleMessageDeletedAsync;
        // Fire-and-forget: ticket buttons create channels and read message
        // history (several HTTP calls), which must not block the gateway task.
        _client.ButtonExecuted += component =>
        {
            _ = HandleButtonAsync(component);
            return Task.CompletedTask;
        };
        // Fire-and-forget: the log-setup menu creates channels (several HTTP
        // calls), which must not block the gateway task.
        _client.SelectMenuExecuted += component =>
        {
            _ = HandleSelectMenuAsync(component);
            return Task.CompletedTask;
        };
        _client.UserJoined += _autoRoleService.HandleUserJoinedAsync;
        // Runs after the auto-role handler so the greeting lands once the new
        // member already holds their join roles.
        _client.UserJoined += _welcomeService.HandleUserJoinedAsync;
        _client.UserVoiceStateUpdated += _autoRoleService.HandleVoiceStateUpdatedAsync;

        // Logging service — fire-and-forget so slow audit-log lookups (1.2s
        // delay + HTTP) and channel/message HTTP calls run detached and never
        // block the gateway task. Each handler self-gates on the guild's config
        // and catches its own errors.
        _client.MessageDeleted += (message, channel) =>
        {
            _ = _logService.HandleMessageDeletedAsync(message, channel);
            return Task.CompletedTask;
        };
        _client.MessageUpdated += (before, after, channel) =>
        {
            _ = _logService.HandleMessageUpdatedAsync(before, after, channel);
            return Task.CompletedTask;
        };
        _client.MessagesBulkDeleted += (messages, channel) =>
        {
            _ = _logService.HandleMessagesBulkDeletedAsync(messages, channel);
            return Task.CompletedTask;
        };
        _client.UserJoined += user =>
        {
            _ = _logService.HandleUserJoinedAsync(user);
            return Task.CompletedTask;
        };
        _client.UserLeft += (guild, user) =>
        {
            _ = _logService.HandleUserLeftAsync(guild, user);
            return Task.CompletedTask;
        };
        _client.UserBanned += (user, guild) =>
        {
            _ = _logService.HandleUserBannedAsync(user, guild);
            return Task.CompletedTask;
        };
        _client.UserUnbanned += (user, guild) =>
        {
            _ = _logService.HandleUserUnbannedAsync(user, guild);
            return Task.CompletedTask;
        };
        _client.UserVoiceStateUpdated += (user, before, after) =>
        {
            _ = _logService.HandleVoiceStateUpdatedAsync(user, before, after);
            return Task.CompletedTask;
        };
        _client.GuildMemberUpdated += (before, after) =>
        {
            _ = _logService.HandleGuildMemberUpdatedAsync(before, after);
            return Task.CompletedTask;
        };
        _client.ChannelCreated += channel =>
        {
            _ = _logService.HandleChannelCreatedAsync(channel);
            return Task.CompletedTask;
        };
        _client.ChannelDestroyed += channel =>
        {
            _ = _logService.HandleChannelDestroyedAsync(channel);
            // Drops the record for a ticket channel deleted outside the bot, so
            // the opener's allowance frees up again.
            _ = _ticketService.HandleChannelDestroyedAsync(channel);
            return Task.CompletedTask;
        };
        _client.ChannelUpdated += (before, after) =>
        {
            _ = _logService.HandleChannelUpdatedAsync(before, after);
            return Task.CompletedTask;
        };
        _client.RoleCreated += role =>
        {
            _ = _logService.HandleRoleCreatedAsync(role);
            return Task.CompletedTask;
        };
        _client.RoleDeleted += role =>
        {
            _ = _logService.HandleRoleDeletedAsync(role);
            return Task.CompletedTask;
        };
        _client.RoleUpdated += (before, after) =>
        {
            _ = _logService.HandleRoleUpdatedAsync(before, after);
            return Task.CompletedTask;
        };

        // Voice XP — detached like the log handlers; the service self-gates on
        // the guild's leveling config and catches its own errors.
        _client.UserVoiceStateUpdated += (user, before, after) =>
        {
            _ = _levelService.HandleVoiceStateUpdatedAsync(user, before, after);
            return Task.CompletedTask;
        };
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

        if (await _autoModService.ScanAsync(message))
            return;

        // Media-only channels drop everything without an attachment, sticker or
        // link — commands included — so this runs before the prefix is parsed.
        if (await _mediaService.ScanAsync(message))
            return;

        var argumentPosition = 0;
        var guildId = (message.Channel as SocketGuildChannel)?.Guild.Id;
        var prefix = _prefixService.GetPrefix(guildId);
        var hasPrefix = message.HasStringPrefix(
            prefix,
            ref argumentPosition);

        // A no-prefix member's raw message runs as a command only when its first word
        // is a real command name. Anything else has to stay ordinary chat, otherwise
        // they would silently stop earning XP and stop hitting the autoresponder.
        var usesNoPrefix =
            !hasPrefix &&
            _noPrefixService.IsAllowed(message.Author.Id) &&
            IsKnownCommand(GetCommandName(message.Content, 0));

        // Argument position stays at zero on the no-prefix path, which is exactly
        // where the command name starts when there is nothing to skip.
        var isCommand = hasPrefix || usesNoPrefix;

        var commandName = isCommand
            ? GetCommandName(message.Content, argumentPosition)
            : string.Empty;

        if (!IsAfkCommand(commandName))
        {
            await ClearAfkIfNeededAsync(message);
        }

        await NotifyMentionedAfkUsersAsync(message);

        if (!isCommand && MentionsCurrentBot(message))
        {
            await SendMentionCardAsync(message);
            return;
        }

        if (!isCommand)
        {
            // Not a command — award chat XP and let the autoresponder look for a
            // trigger match. XP is detached so a Mongo write never delays the reply.
            _ = _levelService.HandleTextMessageAsync(message);
            await _autoResponderService.HandleMessageAsync(message);
            return;
        }

        var context = new SocketCommandContext(_client, message);

        var result = await _commands.ExecuteAsync(
            context,
            argumentPosition,
            _services);

        if (result.IsSuccess)
            return;

        if (result.Error is CommandError.UnknownCommand)
        {
            // The command service only knows the commands declared at compile
            // time, so an unknown name may still be one of this guild's dynamic
            // role commands created with `setuprolecreate`.
            await _setupRoleService.TryHandleCommandAsync(
                message,
                commandName,
                argumentPosition);

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

    /// <summary>
    /// Whether a bare word matches a registered command name or alias. Only the
    /// no-prefix path asks, because a member who skips the prefix must not turn every
    /// sentence they type into a command attempt.
    /// </summary>
    private bool IsKnownCommand(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return _commands.Commands.Any(command =>
            command.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            command.Aliases.Any(alias =>
                alias.Equals(name, StringComparison.OrdinalIgnoreCase)));
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
        // Hidden commands are counted here: they exist and are runnable, they just are
        // never listed. The second figure is scanned against this member's permissions.
        var commandCount = _commandAccessService.TotalCommands;
        var availableCommands = _commandAccessService.CountAvailable(message.Author);
        var guildId = (message.Channel as SocketGuildChannel)?.Guild.Id;

        return message.Channel.SendMessageAsync(
            allowedMentions: AllowedMentions.None,
            components: _mentionComponentBuilder.Build(
                botName,
                botAvatarUrl,
                _prefixService.GetPrefix(guildId),
                commandCount,
                availableCommands));
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

        if (BanListComponentIds.TryParse(
                component.Data.CustomId,
                out var banListAction,
                out var banListSessionId,
                out var banListPage,
                out var banListTargetId))
        {
            await HandleBanListButtonAsync(
                component,
                banListAction,
                banListSessionId,
                banListPage,
                banListTargetId);
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

        if (LevelComponentIds.TryParse(
                component.Data.CustomId,
                out var levelBoard,
                out var levelPage,
                out var levelRequesterId,
                out var levelGuildId))
        {
            await HandleLeaderboardButtonAsync(
                component,
                levelBoard,
                levelPage,
                levelRequesterId,
                levelGuildId);
            return;
        }

        if (TicketComponentIds.TryParse(
                component.Data.CustomId,
                out var ticketAction,
                out var ticketTarget))
        {
            await HandleTicketButtonAsync(component, ticketAction, ticketTarget);
            return;
        }

        if (GiveawayComponentIds.TryParse(
                component.Data.CustomId,
                out var giveawayAction,
                out var giveawayMessageId,
                out var giveawayPage,
                out var giveawayRequesterId))
        {
            await HandleGiveawayButtonAsync(
                component,
                giveawayAction,
                giveawayMessageId,
                giveawayPage,
                giveawayRequesterId);
            return;
        }

        if (NoPrefixComponentIds.TryParseListNav(
                component.Data.CustomId,
                out var noPrefixPage,
                out var noPrefixRequesterId))
        {
            await HandleNoPrefixListButtonAsync(
                component,
                noPrefixPage,
                noPrefixRequesterId);
            return;
        }

        if (MusicComponentIds.TryParseControl(
                component.Data.CustomId,
                out var musicControl))
        {
            await HandleMusicControlButtonAsync(component, musicControl);
            return;
        }

        if (MusicComponentIds.TryParseQueueNav(
                component.Data.CustomId,
                out var musicPage,
                out var musicRequesterId))
        {
            await HandleMusicQueueButtonAsync(
                component,
                musicPage,
                musicRequesterId);
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
            Console.WriteLine($"[Button Error] {DiscordFailure.Format(exception)}");

            if (!component.HasResponded)
            {
                await component.RespondAsync(
                    "Stats panel update failed.",
                    ephemeral: true);
            }
        }
    }

    private async Task HandleLeaderboardButtonAsync(
        SocketMessageComponent component,
        LevelBoard track,
        int page,
        ulong requesterId,
        ulong guildId)
    {
        if (component.User.Id != requesterId)
        {
            await component.RespondAsync(
                "Only the user who opened this leaderboard can control it.",
                ephemeral: true);
            return;
        }

        var guild = _client.GetGuild(guildId);

        if (guild is null)
        {
            await component.RespondAsync(
                "This leaderboard is no longer available.",
                ephemeral: true);
            return;
        }

        // The XP cache is live, so every page flip re-sorts current standings.
        var ranked = track == LevelBoard.Voice
            ? _levelService.GetVoiceLeaderboard(guildId)
            : _levelService.GetTextLeaderboard(guildId);

        await component.UpdateAsync(properties =>
        {
            properties.AllowedMentions = AllowedMentions.None;
            properties.Components = _levelComponentBuilder.BuildLeaderboard(
                track,
                ranked,
                guild,
                page,
                requesterId);
        });
    }

    /// <summary>
    /// Routes the giveaway buttons. Enter is open to every member, so its id carries
    /// no requester id and the live state is rechecked here instead; the entry pages
    /// stay locked to whoever ran the command.
    /// </summary>
    private async Task HandleGiveawayButtonAsync(
        SocketMessageComponent component,
        GiveawayAction action,
        ulong messageId,
        int page,
        ulong requesterId)
    {
        try
        {
            if (action == GiveawayAction.Entries)
            {
                await HandleGiveawayEntriesButtonAsync(
                    component,
                    messageId,
                    page,
                    requesterId);

                return;
            }

            if (component.User is not SocketGuildUser member)
            {
                await component.RespondAsync(
                    "Giveaways can only be entered from inside the server.",
                    ephemeral: true);

                return;
            }

            var outcome = await _giveawayService.ToggleEntryAsync(messageId, member);

            // The card's entry count is refreshed in batches by the giveaway ticker,
            // so this ephemeral reply is what makes the click feel immediate.
            await component.RespondAsync(
                outcome.Result switch
                {
                    GiveawayEntryResult.Done when outcome.Joined =>
                        $"🎉 You have entered this giveaway — `{outcome.EntryCount}` " +
                        "entry(s) so far. Press the button again to leave.",
                    GiveawayEntryResult.Done =>
                        $"You have left this giveaway — `{outcome.EntryCount}` " +
                        "entry(s) left.",
                    GiveawayEntryResult.Ended =>
                        "This giveaway has already ended.",
                    _ => "This giveaway is no longer being tracked."
                },
                ephemeral: true);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Giveaway Button Error] {DiscordFailure.Summarize(exception)}");
        }
    }

    private async Task HandleGiveawayEntriesButtonAsync(
        SocketMessageComponent component,
        ulong messageId,
        int page,
        ulong requesterId)
    {
        if (component.User.Id != requesterId)
        {
            await component.RespondAsync(
                "Only the user who opened this entry list can control it.",
                ephemeral: true);
            return;
        }

        if (_giveawayService.GetGiveaway(messageId) is not { } giveaway ||
            _client.GetGuild(giveaway.GuildId) is not { } guild)
        {
            await component.RespondAsync(
                "This giveaway is no longer available.",
                ephemeral: true);
            return;
        }

        // Entries are read fresh on every flip, so a page shows who is in right now
        // rather than who was in when the command ran.
        await component.UpdateAsync(properties =>
        {
            properties.AllowedMentions = AllowedMentions.None;
            properties.Components = _giveawayComponentBuilder.BuildEntries(
                giveaway,
                guild,
                page,
                requesterId);
        });
    }

    /// <summary>
    /// Flips a page of the queue listing. The page number travels in the custom id but
    /// the tracks do not, so a card left open still lists what is actually queued.
    /// </summary>
    private async Task HandleMusicQueueButtonAsync(
        SocketMessageComponent component,
        int page,
        ulong requesterId)
    {
        if (component.User.Id != requesterId)
        {
            await component.RespondAsync(
                "Only the user who opened this queue can control it.",
                ephemeral: true);
            return;
        }

        var guild = (component.Channel as SocketGuildChannel)?.Guild;

        if (guild is null)
            return;

        // The queue is read fresh on every page flip rather than carried in the custom
        // id, so a card left open for an hour still shows what is actually queued.
        var player = await _musicService.GetExistingPlayerAsync(guild.Id);

        if (player is null)
        {
            await component.RespondAsync(
                "I am not in a voice channel anymore, so there is no queue to show.",
                ephemeral: true);
            return;
        }

        await component.UpdateAsync(properties =>
        {
            properties.AllowedMentions = AllowedMentions.None;
            properties.Components = _musicComponentBuilder.BuildQueuePage(
                player.CurrentTrack,
                player.Position?.Position,
                MusicService.Snapshot(player),
                page,
                requesterId,
                player.RepeatMode,
                player.Shuffle);
        });
    }

    /// <summary>
    /// Play, pause and skip on the player panel. The card carries no requester id because
    /// these buttons steer the whole channel's playback, so the gate is standing in the
    /// bot's voice channel rather than owning the message.
    /// </summary>
    private async Task HandleMusicControlButtonAsync(
        SocketMessageComponent component,
        MusicControl control)
    {
        var guild = (component.Channel as SocketGuildChannel)?.Guild;

        if (guild is null)
            return;

        var player = await _musicService.GetExistingPlayerAsync(guild.Id);

        if (player?.CurrentTrack is null)
        {
            await component.RespondAsync(
                "Nothing is playing anymore, so these controls do nothing.",
                ephemeral: true);
            return;
        }

        if (guild.GetUser(component.User.Id)?.VoiceChannel?.Id != player.VoiceChannelId)
        {
            await component.RespondAsync(
                "You have to be in my voice channel to control playback.",
                ephemeral: true);
            return;
        }

        if (control is MusicControl.Skip)
        {
            await HandleMusicSkipButtonAsync(component, guild.Id, player);
            return;
        }

        if (control is MusicControl.Pause)
            await player.PauseAsync();
        else
            await player.ResumeAsync();

        // Captured before the update lambda: the property is nullable, and reading it
        // inside the callback would leave the compiler unable to prove it is still set.
        var track = player.CurrentTrack;

        await component.UpdateAsync(properties =>
        {
            properties.AllowedMentions = AllowedMentions.None;
            properties.Components = _musicComponentBuilder.BuildNowPlaying(
                track,
                player.Volume,
                player.RepeatMode,
                player.Shuffle,
                player.Queue.Count,
                player.State is PlayerState.Paused,
                null);
        });
    }

    /// <summary>
    /// Skip is deferred rather than answered with an edit: the track change posts a fresh
    /// panel on its own and deletes this one, so editing it here would only race that.
    /// The card is cleaned up by hand in the one case where no replacement is coming.
    /// </summary>
    private async Task HandleMusicSkipButtonAsync(
        SocketMessageComponent component,
        ulong guildId,
        QueuedLavalinkPlayer player)
    {
        await component.DeferAsync();
        await player.SkipAsync();

        if (player.CurrentTrack is not null)
            return;

        await _musicService.DeletePlayerCardAsync(guildId);

        await component.Channel.SendMessageAsync(
            allowedMentions: AllowedMentions.None,
            components: _musicComponentBuilder.BuildResult(
                "Skipped",
                "That was the last track, so the queue is now empty."));
    }

    private async Task HandleNoPrefixListButtonAsync(
        SocketMessageComponent component,
        int page,
        ulong requesterId)
    {
        if (component.User.Id != requesterId)
        {
            await component.RespondAsync(
                "Only the user who opened this list can control it.",
                ephemeral: true);
            return;
        }

        var entries = _noPrefixService.GetAll();
        var guild = (component.Channel as SocketGuildChannel)?.Guild;
        var users = new Dictionary<ulong, IUser?>();

        foreach (var entry in entries)
        {
            if (users.ContainsKey(entry.UserId))
                continue;

            users[entry.UserId] =
                guild?.GetUser(entry.UserId) as IUser ??
                _client.GetUser(entry.UserId);
        }

        await component.UpdateAsync(properties =>
        {
            properties.AllowedMentions = AllowedMentions.None;
            properties.Components = _noPrefixComponentBuilder.BuildList(
                entries,
                users,
                page,
                requesterId);
        });
    }

    /// <summary>
    /// Routes the ticket panel and in-ticket buttons. The create button is open to
    /// every member, so its id carries no requester id; access for the staff
    /// actions is checked inside the service against the guild's support roles.
    /// </summary>
    private async Task HandleTicketButtonAsync(
        SocketMessageComponent component,
        TicketAction action,
        ulong targetId)
    {
        try
        {
            if (action == TicketAction.New)
            {
                await HandleTicketCreateButtonAsync(component, targetId);
                return;
            }

            if (component.User is not SocketGuildUser member ||
                member.Guild.GetTextChannel(targetId) is not { } channel)
            {
                await component.RespondAsync(
                    "This ticket is no longer available.",
                    ephemeral: true);

                return;
            }

            // Delete destroys the channel this interaction lives in, so it has to
            // answer first — afterwards there is nowhere left to reply.
            if (action == TicketAction.Delete)
            {
                await component.RespondAsync("Deleting this ticket…", ephemeral: true);

                var deleted = await _ticketService.DeleteAsync(member, channel);

                if (deleted.Result != TicketActionResult.Done)
                {
                    await component.FollowupAsync(
                        DescribeTicketFailure(deleted.Result),
                        ephemeral: true);
                }

                return;
            }

            await component.DeferAsync();

            var outcome = action switch
            {
                TicketAction.Close =>
                    await _ticketService.CloseAsync(member, channel, reason: null),
                TicketAction.Claim =>
                    await _ticketService.SetClaimAsync(member, channel, claim: true),
                TicketAction.Unclaim =>
                    await _ticketService.SetClaimAsync(member, channel, claim: false),
                TicketAction.Reopen =>
                    await _ticketService.ReopenAsync(member, channel),
                _ => await _ticketService.SaveTranscriptAsync(member, channel)
            };

            if (outcome.Result != TicketActionResult.Done)
            {
                await component.FollowupAsync(
                    DescribeTicketFailure(outcome.Result),
                    ephemeral: true);

                return;
            }

            // Close and Transcript already post their own card, so only claim,
            // unclaim, and reopen need a visible note for the rest of the staff.
            switch (action)
            {
                case TicketAction.Claim:
                    await channel.SendMessageAsync(
                        allowedMentions: AllowedMentions.None,
                        components: _ticketComponentBuilder.BuildActionCard(
                            "Ticket Claimed",
                            $"{member.Mention} is handling this ticket."));
                    break;

                case TicketAction.Unclaim:
                    await channel.SendMessageAsync(
                        allowedMentions: AllowedMentions.None,
                        components: _ticketComponentBuilder.BuildActionCard(
                            "Ticket Released",
                            "This ticket is unclaimed again — any staff member can " +
                            "take it."));
                    break;

                case TicketAction.Reopen:
                    await channel.SendMessageAsync(
                        allowedMentions: AllowedMentions.None,
                        components: _ticketComponentBuilder.BuildActionCard(
                            "Ticket Reopened",
                            $"**Reopened by:** {member.Mention}",
                            "The member who opened it can see and post here again."));
                    break;
            }

            await component.FollowupAsync(
                action switch
                {
                    TicketAction.Close => "Ticket closed.",
                    TicketAction.Claim => "You claimed this ticket.",
                    TicketAction.Unclaim => "You released this ticket.",
                    TicketAction.Reopen => "Ticket reopened.",
                    _ => "Transcript saved to the ticket log channel."
                },
                ephemeral: true);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Ticket Button Error] {DiscordFailure.Summarize(exception)}");
        }
    }

    private async Task HandleTicketCreateButtonAsync(
        SocketMessageComponent component,
        ulong guildId)
    {
        // Creating a channel is several HTTP calls, well past the three-second
        // interaction window, so the click is acknowledged first.
        await component.DeferAsync();

        if (component.User is not SocketGuildUser member || member.Guild.Id != guildId)
        {
            await component.FollowupAsync(
                "This panel belongs to another server.",
                ephemeral: true);

            return;
        }

        var outcome = await _ticketService.OpenAsync(member, reason: null);

        var message = outcome.Result switch
        {
            TicketOpenResult.Opened =>
                $"Your ticket is open: <#{outcome.ChannelId}>",

            TicketOpenResult.Disabled =>
                "The ticket system is switched off in this server.",

            TicketOpenResult.CategoryMissing =>
                "Tickets are not set up yet — ask an admin to run the ticket setup.",

            TicketOpenResult.MissingBotPermission =>
                "I need `Manage Channels` or `Administrator` to create ticket " +
                "channels.",

            TicketOpenResult.LimitReached =>
                $"You already have `{outcome.Limit}` ticket(s) open — yours is " +
                $"<#{outcome.ChannelId}>.",

            _ => "Creating your ticket failed. Please try again in a moment."
        };

        await component.FollowupAsync(
            message,
            ephemeral: true,
            allowedMentions: AllowedMentions.None);
    }

    private static string DescribeTicketFailure(TicketActionResult result)
    {
        return result switch
        {
            TicketActionResult.NotATicket => "This channel is not a ticket.",

            TicketActionResult.MissingAccess =>
                "You need a ticket support role, `Manage Channels`, or " +
                "`Administrator` for that.",

            TicketActionResult.AlreadyClosed => "This ticket is already closed.",

            TicketActionResult.NotClosed => "This ticket is not closed.",

            TicketActionResult.AlreadyClaimed => "This ticket is already claimed.",

            TicketActionResult.NotClaimed => "Nobody has claimed this ticket yet.",

            TicketActionResult.NoLogChannel =>
                "No ticket log channel is set, so there is nowhere to send the " +
                "transcript.",

            _ => "Something went wrong — check that I still have `Manage Channels` " +
                 "here."
        };
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
            Console.WriteLine($"[Unban Button Error] {DiscordFailure.Format(exception)}");

            await component.FollowupAsync(
                DiscordFailure.Describe(
                    exception,
                    "Unban failed. Check my permissions."),
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

    private async Task HandleBanListButtonAsync(
        SocketMessageComponent component,
        BanListAction action,
        string sessionId,
        int page,
        ulong targetId)
    {
        if (!_banListService.TryGet(sessionId, out var session))
        {
            await component.RespondAsync(
                "This ban list has expired. Run the command again.",
                ephemeral: true);
            return;
        }

        if (component.User.Id != session.RequesterId)
        {
            await component.RespondAsync(
                "Only the user who opened this ban list can control it.",
                ephemeral: true);
            return;
        }

        // Pure navigation needs no API call.
        if (action is BanListAction.Previous or BanListAction.Next)
        {
            await component.UpdateAsync(properties =>
            {
                properties.AllowedMentions = AllowedMentions.None;
                properties.Components = _banListComponentBuilder.Build(
                    session.Bans,
                    sessionId,
                    session.RequesterId,
                    page);
            });
            return;
        }

        await component.DeferAsync();

        try
        {
            var guild = _client.GetGuild(session.GuildId);
            var moderator = guild?.GetUser(session.RequesterId);

            if (guild is null || moderator is null)
            {
                await component.FollowupAsync(
                    "I could not fetch the server or moderator.",
                    ephemeral: true);
                return;
            }

            var validationError = ValidateUnbanRequest(guild, moderator);

            if (validationError is not null)
            {
                await component.FollowupAsync(
                    validationError,
                    ephemeral: true);
                return;
            }

            if (action == BanListAction.Unban)
            {
                await TryRemoveBanAsync(guild, targetId);
                _banListService.RemoveUser(sessionId, targetId);
            }
            else if (action == BanListAction.UnbanAll)
            {
                foreach (var ban in session.Bans.ToArray())
                    await TryRemoveBanAsync(guild, ban.UserId);

                _banListService.ClearUsers(sessionId);
            }

            await component.ModifyOriginalResponseAsync(properties =>
            {
                properties.AllowedMentions = AllowedMentions.None;
                properties.Components = _banListComponentBuilder.Build(
                    session.Bans,
                    sessionId,
                    session.RequesterId,
                    page);
            });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[BanList Button Error] {DiscordFailure.Format(exception)}");

            await component.FollowupAsync(
                DiscordFailure.Describe(
                    exception,
                    "Ban list update failed. Check my permissions."),
                ephemeral: true);
        }
    }

    private static async Task TryRemoveBanAsync(SocketGuild guild, ulong userId)
    {
        try
        {
            await guild.RemoveBanAsync(userId);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[BanList Unban Error] {userId}: {DiscordFailure.Format(exception)}");
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
            Console.WriteLine($"[Ban Button Error] {DiscordFailure.Format(exception)}");

            await component.FollowupAsync(
                DiscordFailure.Describe(
                    exception,
                    "Ban failed. Check my permissions and role position."),
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

            await _logService.LogKickAsync(
                guild,
                targetMember,
                moderator,
                request.Reason);

            await component.ModifyOriginalResponseAsync(properties =>
            {
                properties.AllowedMentions = AllowedMentions.None;
                properties.Components = _kickComponentBuilder.BuildSuccess(
                    request);
            });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Kick Button Error] {DiscordFailure.Format(exception)}");

            await component.FollowupAsync(
                DiscordFailure.Describe(
                    exception,
                    "Kick failed. Check my permissions and role position."),
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
            Console.WriteLine($"[Nuke Button Error] {DiscordFailure.Format(exception)}");

            await component.FollowupAsync(
                DiscordFailure.Describe(
                    exception,
                    "Nuke failed. Check my permissions and role position."),
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
            Console.WriteLine($"[Role Info Button Error] {DiscordFailure.Format(exception)}");

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
            Console.WriteLine($"[Server Info Button Error] {DiscordFailure.Format(exception)}");

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
            Console.WriteLine($"[Avatar Button Error] {DiscordFailure.Format(exception)}");

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
        if (VoiceComponentIds.TryParseMenu(
                component.Data.CustomId,
                out var voiceOp,
                out var voiceRequesterId,
                out var voiceGuildId,
                out var voiceTargetId))
        {
            await HandleVoiceMenuSelectAsync(
                component,
                voiceOp,
                voiceRequesterId,
                voiceGuildId,
                voiceTargetId);
            return;
        }

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

        if (AutoModComponentIds.TryParseRulesMenu(
                component.Data.CustomId,
                out var autoModRequesterId,
                out var autoModGuildId))
        {
            await HandleAutoModRulesSelectAsync(
                component,
                autoModRequesterId,
                autoModGuildId);
            return;
        }

        if (LogComponentIds.TryParseSetupMenu(
                component.Data.CustomId,
                out var logRequesterId,
                out var logGuildId))
        {
            await HandleLogSetupSelectAsync(
                component,
                logRequesterId,
                logGuildId);
            return;
        }

        if (NoPrefixComponentIds.TryParseDurationMenu(
                component.Data.CustomId,
                out var noPrefixTargetId,
                out var noPrefixRequesterId))
        {
            await HandleNoPrefixDurationSelectAsync(
                component,
                noPrefixTargetId,
                noPrefixRequesterId);
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
            var guildId = (component.Channel as SocketGuildChannel)?.Guild.Id;
            var components = _helpComponentBuilder.Build(
                userId,
                _prefixService.GetPrefix(guildId),
                component.User.Mention,
                _client.CurrentUser.Username,
                _client.CurrentUser.GetDisplayAvatarUrl(size: 256),
                _commandAccessService.TotalCommands,
                _commandAccessService.CountAvailable(component.User),
                category);

            await component.UpdateAsync(
                properties => properties.Components = components);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Select Menu Error] {DiscordFailure.Format(exception)}");

            if (!component.HasResponded)
            {
                await component.RespondAsync(
                    "Help menu update failed.",
                    ephemeral: true);
            }
        }
    }

    private async Task HandleAutoModRulesSelectAsync(
        SocketMessageComponent component,
        ulong requesterId,
        ulong guildId)
    {
        if (component.User.Id != requesterId)
        {
            await component.RespondAsync(
                "Only the user who enabled AutoMod can use this menu.",
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

            if (!(moderator.GuildPermissions.ManageGuild ||
                  moderator.GuildPermissions.Administrator))
            {
                await component.FollowupAsync(
                    "You no longer have permission to manage AutoMod.",
                    ephemeral: true);
                return;
            }

            var selectedRules = new HashSet<AutoModRuleType>();

            foreach (var value in component.Data.Values)
            {
                if (Enum.TryParse<AutoModRuleType>(value, out var rule))
                    selectedRules.Add(rule);
            }

            await _autoModConfigService.SetRulesEnabledAsync(
                guildId,
                selectedRules);

            var config = _autoModConfigService.GetConfig(guildId);
            var prefix = _prefixService.GetPrefix(guildId);

            await component.ModifyOriginalResponseAsync(properties =>
            {
                properties.AllowedMentions = AllowedMentions.None;
                properties.Components =
                    _autoModComponentBuilder.BuildRulesConfigurator(
                        "AutoMod Rules Updated",
                        "Selected rules are enabled; unselected ones are disabled.",
                        config,
                        requesterId,
                        guildId,
                        _autoModConfigService.IsPersistent,
                        prefix);
            });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[AutoMod Rules Select Error] {DiscordFailure.Format(exception)}");

            await component.FollowupAsync(
                "Updating AutoMod rules failed.",
                ephemeral: true);
        }
    }

    private async Task HandleLogSetupSelectAsync(
        SocketMessageComponent component,
        ulong requesterId,
        ulong guildId)
    {
        if (component.User.Id != requesterId)
        {
            await component.RespondAsync(
                "Only the user who enabled logging can use this menu.",
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

            if (!(moderator.GuildPermissions.ManageGuild ||
                  moderator.GuildPermissions.Administrator))
            {
                await component.FollowupAsync(
                    "You no longer have permission to manage logging.",
                    ephemeral: true);
                return;
            }

            var bot = guild.CurrentUser;

            if (!(bot.GuildPermissions.ManageChannels ||
                  bot.GuildPermissions.Administrator))
            {
                await component.FollowupAsync(
                    "I need the `Manage Channels` permission to create log channels.",
                    ephemeral: true);
                return;
            }

            var selectedTypes = new HashSet<LogType>();

            foreach (var value in component.Data.Values)
            {
                if (Enum.TryParse<LogType>(value, out var type))
                    selectedTypes.Add(type);
            }

            // Snapshot the config before mutating so the create/clear decisions
            // read the pre-selection state (SetChannelAsync swaps the cache entry
            // for a fresh clone, leaving this reference frozen).
            var current = _logConfigService.GetConfig(guildId);

            var toCreate = Enum.GetValues<LogType>()
                .Where(type => selectedTypes.Contains(type))
                .Where(type =>
                    current.GetChannel(type) is not { } existing ||
                    guild.GetTextChannel(existing) is null)
                .ToList();

            // Group the auto-created channels under a single "Logs" category.
            var categoryId = toCreate.Count > 0
                ? await ResolveLogCategoryIdAsync(guild)
                : null;

            foreach (var type in toCreate)
            {
                var created = await guild.CreateTextChannelAsync(
                    DefaultLogChannelName(type),
                    properties =>
                    {
                        if (categoryId is { } id)
                            properties.CategoryId = id;

                        // Private from birth: deny @everyone view, keep the bot
                        // in — no public window between create and lock-down.
                        properties.PermissionOverwrites =
                            BuildPrivateLogOverwrites(guild);
                    },
                    new RequestOptions
                    {
                        AuditLogReason =
                            $"Log channel created by {moderator.Username}"
                    });

                await _logConfigService.SetChannelAsync(guildId, type, created.Id);
            }

            // Deselecting a type stops logging it. Only the mapping is cleared —
            // the channel itself is left in place to avoid destroying history.
            foreach (var type in Enum.GetValues<LogType>())
            {
                if (!selectedTypes.Contains(type) &&
                    current.GetChannel(type) is not null)
                {
                    await _logConfigService.SetChannelAsync(guildId, type, null);
                }
            }

            await _logConfigService.SetEnabledAsync(guildId, true);

            var config = _logConfigService.GetConfig(guildId);
            var prefix = _prefixService.GetPrefix(guildId);

            await component.ModifyOriginalResponseAsync(properties =>
            {
                properties.AllowedMentions = AllowedMentions.None;
                properties.Components = _logComponentBuilder.BuildSetupConfigurator(
                    "Logging Updated",
                    "Selected types now log to their channels; deselected types are off.",
                    config,
                    requesterId,
                    guildId,
                    _logConfigService.IsPersistent,
                    prefix);
            });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Logs Setup Select Error] {DiscordFailure.Format(exception)}");

            await component.FollowupAsync(
                DiscordFailure.Describe(
                    exception,
                    "Updating logging failed. Check my permissions and role position."),
                ephemeral: true);
        }
    }

    /// <summary>
    /// Applies the duration picked from the no-prefix dropdown. The gate is the owner
    /// check rather than the requester id, so a stale card from an older session still
    /// answers to the owner and to nobody else.
    /// </summary>
    private async Task HandleNoPrefixDurationSelectAsync(
        SocketMessageComponent component,
        ulong targetId,
        ulong requesterId)
    {
        // An ephemeral refusal is fine here even though the command itself is silent:
        // the dropdown is already visible in the channel, so there is nothing left to
        // hide, and answering nothing would leave Discord showing "Interaction failed".
        if (!_noPrefixService.IsOwner(component.User.Id))
        {
            await component.RespondAsync(
                "You cannot use this menu.",
                ephemeral: true);
            return;
        }

        await component.DeferAsync();

        try
        {
            if (!NoPrefixComponentIds.TryParseDuration(
                    component.Data.Values.FirstOrDefault(),
                    out var duration))
            {
                await component.FollowupAsync(
                    "That duration is no longer available.",
                    ephemeral: true);
                return;
            }

            var outcome = await _noPrefixService.GrantAsync(
                targetId,
                requesterId,
                duration);

            if (outcome.Result != NoPrefixGrantResult.Granted ||
                outcome.Entry is not { } entry)
            {
                await component.FollowupAsync(
                    $"I already track {NoPrefixService.MaxTrackedUsers} members. " +
                    "Remove one before adding another.",
                    ephemeral: true);
                return;
            }

            var guild = (component.Channel as SocketGuildChannel)?.Guild;

            var target =
                guild?.GetUser(targetId) as IUser ??
                _client.GetUser(targetId);

            await component.ModifyOriginalResponseAsync(properties =>
            {
                properties.AllowedMentions = AllowedMentions.None;
                properties.Components =
                    _noPrefixComponentBuilder.BuildGrantConfirmation(
                        entry,
                        target,
                        outcome.Persisted);
            });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[NoPrefix Select Error] {DiscordFailure.Format(exception)}");

            await component.FollowupAsync(
                "Granting no-prefix access failed. Try again.",
                ephemeral: true);
        }
    }

    private static async Task<ulong?> ResolveLogCategoryIdAsync(SocketGuild guild)
    {
        var existing = guild.CategoryChannels.FirstOrDefault(category =>
            category.Name.Equals("Logs", StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
            return existing.Id;

        // Only a freshly created category is locked down; an existing "Logs"
        // category is left exactly as the server configured it.
        var created = await guild.CreateCategoryChannelAsync(
            "Logs",
            properties =>
            {
                properties.PermissionOverwrites =
                    BuildPrivateLogOverwrites(guild);
            },
            new RequestOptions
            {
                AuditLogReason = "Logs category for server logging channels"
            });

        return created?.Id;
    }

    // Log channels/category are created private: @everyone loses view, and the
    // bot gets an explicit allow so it can still see and post there (needed when
    // the bot lacks Administrator, where the @everyone deny would hide it too).
    private static Overwrite[] BuildPrivateLogOverwrites(SocketGuild guild)
    {
        return new[]
        {
            new Overwrite(
                guild.EveryoneRole.Id,
                PermissionTarget.Role,
                new OverwritePermissions(viewChannel: PermValue.Deny)),
            new Overwrite(
                guild.CurrentUser.Id,
                PermissionTarget.User,
                new OverwritePermissions(
                    viewChannel: PermValue.Allow,
                    sendMessages: PermValue.Allow))
        };
    }

    private static string DefaultLogChannelName(LogType type)
    {
        return type switch
        {
            LogType.Messages => "message-logs",
            LogType.Members => "member-logs",
            LogType.Voice => "voice-logs",
            LogType.Moderation => "moderation-logs",
            LogType.Roles => "role-logs",
            LogType.Server => "server-logs",
            _ => "server-logs"
        };
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
                        $"[UnhideAll Error] #{channel.Name} ({channel.Id}): {DiscordFailure.Summarize(exception)}");
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
            Console.WriteLine($"[UnhideAll Select Error] {DiscordFailure.Format(exception)}");

            await component.FollowupAsync(
                DiscordFailure.Describe(
                    exception,
                    "Unhide all failed. Check my permissions and role position."),
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

                    // Kept in step with the unlock command: an outright allow, not a
                    // clear back to inherit, so the channel is usable afterwards even
                    // where @everyone cannot post at the server or category level.
                    await channel.AddPermissionOverwriteAsync(
                        everyoneRole,
                        overwrite.Modify(sendMessages: PermValue.Allow),
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
                        $"[UnlockAll Error] #{channel.Name} ({channel.Id}): {DiscordFailure.Summarize(exception)}");
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
            Console.WriteLine($"[UnlockAll Select Error] {DiscordFailure.Format(exception)}");

            await component.FollowupAsync(
                DiscordFailure.Describe(
                    exception,
                    "Unlock all failed. Check my permissions and role position."),
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

    private async Task HandleVoiceMenuSelectAsync(
        SocketMessageComponent component,
        string op,
        ulong requesterId,
        ulong guildId,
        ulong targetId)
    {
        if (component.User.Id != requesterId)
        {
            await component.RespondAsync(
                "Only the user who opened this menu can control it.",
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

            if (!HasMoveMembers(moderator.GuildPermissions))
            {
                await component.FollowupAsync(
                    "You no longer have permission to move members.",
                    ephemeral: true);
                return;
            }

            if (!HasMoveMembers(guild.CurrentUser.GuildPermissions))
            {
                await component.FollowupAsync(
                    "I no longer have permission to move members.",
                    ephemeral: true);
                return;
            }

            var selectedValue = component.Data.Values.FirstOrDefault();

            if (selectedValue is null ||
                !VoiceComponentIds.TryParseChannelValue(
                    selectedValue,
                    out var selectedChannelId))
            {
                await ModifyToVoiceNoticeAsync(
                    component,
                    "Invalid Selection",
                    "That selection was not valid.");
                return;
            }

            var selectedChannel = guild.GetVoiceChannel(selectedChannelId);

            if (selectedChannel is null)
            {
                await ModifyToVoiceNoticeAsync(
                    component,
                    "Channel Not Found",
                    "That voice channel no longer exists.");
                return;
            }

            var botId = guild.CurrentUser.Id;

            if (op == "move")
            {
                var target = guild.GetUser(targetId);

                if (target is null || target.VoiceChannel is null)
                {
                    await ModifyToVoiceNoticeAsync(
                        component,
                        "Not In Voice",
                        "That member is no longer connected to a voice channel.");
                    return;
                }

                if (target.VoiceChannel.Id == selectedChannel.Id)
                {
                    await ModifyToVoiceNoticeAsync(
                        component,
                        "Already There",
                        $"<@{target.Id}> is already in that channel.");
                    return;
                }

                await target.ModifyAsync(
                    properties => properties.Channel = selectedChannel,
                    new RequestOptions
                    {
                        AuditLogReason = $"Voice move by {moderator.Username}"
                    });

                await component.ModifyOriginalResponseAsync(properties =>
                {
                    properties.AllowedMentions = AllowedMentions.None;
                    properties.Components = _voiceComponentBuilder.BuildActionResult(
                        "Member Moved",
                        target.Id,
                        moderator.Id,
                        $"**Channel:** <#{selectedChannel.Id}>");
                });
                return;
            }

            // moveall / pullall both need the moderator's live current channel.
            var moderatorChannel = moderator.VoiceChannel;

            if (moderatorChannel is null)
            {
                await ModifyToVoiceNoticeAsync(
                    component,
                    "Not In Voice",
                    "You need to be in a voice channel to use this.");
                return;
            }

            var (source, destination, title) = op == "pullall"
                ? (selectedChannel, moderatorChannel, "Pulled Members (All)")
                : (moderatorChannel, selectedChannel, "Moved Members (All)");

            if (source.Id == destination.Id)
            {
                await ModifyToVoiceNoticeAsync(
                    component,
                    "Same Channel",
                    "Pick a different voice channel.");
                return;
            }

            var (moved, skipped, failed) = await MoveConnectedUsersAsync(
                source,
                destination,
                botId,
                moderator);

            await component.ModifyOriginalResponseAsync(properties =>
            {
                properties.AllowedMentions = AllowedMentions.None;
                properties.Components = _voiceComponentBuilder.BuildBulkResult(
                    title,
                    moved,
                    skipped,
                    failed,
                    moderator.Id,
                    destination.Name);
            });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Voice Select Error] {DiscordFailure.Format(exception)}");

            await component.FollowupAsync(
                DiscordFailure.Describe(
                    exception,
                    "The voice action failed. Check my permissions."),
                ephemeral: true);
        }
    }

    private static async Task<(int Moved, int Skipped, int Failed)> MoveConnectedUsersAsync(
        SocketVoiceChannel source,
        SocketVoiceChannel destination,
        ulong botId,
        SocketGuildUser moderator)
    {
        var moved = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var member in source.ConnectedUsers.ToArray())
        {
            if (member.Id == botId)
                continue;

            if (member.VoiceChannel?.Id == destination.Id)
            {
                skipped++;
                continue;
            }

            try
            {
                await member.ModifyAsync(
                    properties => properties.Channel = destination,
                    new RequestOptions
                    {
                        AuditLogReason = $"Voice move by {moderator.Username}"
                    });

                moved++;
            }
            catch (Exception exception)
            {
                failed++;
                Console.WriteLine(
                    $"[Voice Move Error] {member.Id}: {DiscordFailure.Summarize(exception)}");
            }
        }

        return (moved, skipped, failed);
    }

    private async Task ModifyToVoiceNoticeAsync(
        SocketMessageComponent component,
        string title,
        string message)
    {
        await component.ModifyOriginalResponseAsync(properties =>
        {
            properties.AllowedMentions = AllowedMentions.None;
            properties.Components = _voiceComponentBuilder.BuildNotice(title, message);
        });
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
            Console.WriteLine($"[Snipe Button Error] {DiscordFailure.Format(exception)}");

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
            Console.WriteLine($"[AddRole Remove Button Error] {DiscordFailure.Format(exception)}");

            await component.FollowupAsync(
                DiscordFailure.Describe(
                    exception,
                    "Role removal failed. Check my permissions and role position."),
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

    private static bool HasMoveMembers(GuildPermissions permissions)
    {
        return permissions.MoveMembers || permissions.Administrator;
    }
}
