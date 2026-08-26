using System.Collections.Concurrent;
using System.Text;
using Discord;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Models;
using MongoDB.Driver;

namespace LastRide.Services;

/// <summary>
/// Owns the whole ticket lifecycle: creating the private channel, locking it on
/// close, reopening, deleting, claiming, membership changes, and transcripts.
/// Called by both <see cref="Modules.TicketModule"/> and the panel buttons routed
/// through <see cref="Core.CommandHandler"/>.
/// </summary>
public sealed class TicketService
{
    /// <summary>How far back a transcript reaches; anything older is noted as cut.</summary>
    public const int MaxTranscriptMessages = 500;

    public const int MinChannelNameLength = 2;
    public const int MaxChannelNameLength = 90;

    private const string DefaultOpenMessage =
        "Thanks for reaching out, {user}! A staff member will be with you shortly.\n" +
        "Please describe your issue with as much detail as you can.";

    private const string DefaultPanelMessage =
        "Need a hand from the staff team? Press the button below and a private " +
        "ticket channel will be opened just for you.";

    private const string DatabaseName = "lastride";
    private const string CollectionName = "tickets";

    // Ticket channels are private from birth: @everyone loses view, and the bot
    // gets an explicit allow so it can still see and post there (needed when the
    // bot lacks Administrator, where the @everyone deny would hide it too).
    private static readonly OverwritePermissions BotPermissions = new(
        viewChannel: PermValue.Allow,
        sendMessages: PermValue.Allow,
        manageChannel: PermValue.Allow,
        manageMessages: PermValue.Allow,
        embedLinks: PermValue.Allow,
        attachFiles: PermValue.Allow,
        readMessageHistory: PermValue.Allow);

    private static readonly OverwritePermissions MemberPermissions = new(
        viewChannel: PermValue.Allow,
        sendMessages: PermValue.Allow,
        addReactions: PermValue.Allow,
        embedLinks: PermValue.Allow,
        attachFiles: PermValue.Allow,
        readMessageHistory: PermValue.Allow);

    private static readonly OverwritePermissions SupportPermissions = new(
        viewChannel: PermValue.Allow,
        sendMessages: PermValue.Allow,
        addReactions: PermValue.Allow,
        embedLinks: PermValue.Allow,
        attachFiles: PermValue.Allow,
        manageMessages: PermValue.Allow,
        readMessageHistory: PermValue.Allow);

    private readonly TicketConfigService _configService;
    private readonly TicketComponentBuilder _builder;
    private readonly IMongoCollection<TicketDocument>? _collection;
    private readonly ConcurrentDictionary<ulong, Ticket> _tickets = new();

    public TicketService(
        TicketConfigService configService,
        TicketComponentBuilder builder,
        MongoDbService mongo)
    {
        _configService = configService;
        _builder = builder;
        _collection = mongo.GetCollection<TicketDocument>(
            DatabaseName,
            CollectionName);
    }

    public bool IsPersistent => _collection is not null;

    public async Task LoadAsync()
    {
        if (_collection is null)
            return;

        try
        {
            var documents = await _collection
                .Find(Builders<TicketDocument>.Filter.Empty)
                .ToListAsync();

            foreach (var document in documents)
            {
                var ticket = FromDocument(document);

                if (ticket is not null)
                    _tickets[ticket.ChannelId] = ticket;
            }

            Console.WriteLine($"[Ticket] Loaded {_tickets.Count} ticket(s) from database.");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Ticket Load Error] {exception}");
        }
    }

    public Ticket? GetTicket(ulong channelId)
    {
        return _tickets.TryGetValue(channelId, out var ticket) ? ticket : null;
    }

    public IReadOnlyList<Ticket> GetOpenTickets(ulong guildId)
    {
        return _tickets.Values
            .Where(ticket => ticket.GuildId == guildId && !ticket.IsClosed)
            .OrderBy(ticket => ticket.Number)
            .ToList();
    }

    public bool CanManage(SocketGuildUser member)
    {
        return CanManage(member, _configService.GetConfig(member.Guild.Id));
    }

    /// <summary>The panel text, falling back to the built-in copy.</summary>
    public string RenderPanelMessage(SocketGuild guild)
    {
        var config = _configService.GetConfig(guild.Id);
        var template = config.PanelMessage ?? DefaultPanelMessage;

        return template
            .Replace("{server}", guild.Name)
            .Replace("{membercount}", guild.MemberCount.ToString("N0"));
    }

    /// <summary>
    /// Creates the private ticket channel and posts the opening card inside it.
    /// On <see cref="TicketOpenResult.LimitReached"/> the returned channel id is
    /// the member's oldest open ticket, so the refusal can link it.
    /// </summary>
    public async Task<TicketOpenOutcome> OpenAsync(
        SocketGuildUser member,
        string? reason)
    {
        var guild = member.Guild;
        var config = _configService.GetConfig(guild.Id);

        if (!config.Enabled)
            return new TicketOpenOutcome(TicketOpenResult.Disabled, null, 0, config.Limit);

        if (config.CategoryId is not { } categoryId ||
            guild.CategoryChannels.All(category => category.Id != categoryId))
        {
            return new TicketOpenOutcome(
                TicketOpenResult.CategoryMissing,
                null,
                0,
                config.Limit);
        }

        var bot = guild.CurrentUser;

        if (!(bot.GuildPermissions.ManageChannels ||
              bot.GuildPermissions.Administrator))
        {
            return new TicketOpenOutcome(
                TicketOpenResult.MissingBotPermission,
                null,
                0,
                config.Limit);
        }

        // Stale records for channels deleted while the bot was offline would
        // otherwise eat the member's allowance forever.
        await PruneMissingAsync(guild);

        var open = _tickets.Values
            .Where(ticket =>
                ticket.GuildId == guild.Id &&
                ticket.OwnerId == member.Id &&
                !ticket.IsClosed)
            .OrderBy(ticket => ticket.Number)
            .ToList();

        if (open.Count >= config.Limit)
        {
            return new TicketOpenOutcome(
                TicketOpenResult.LimitReached,
                open[0].ChannelId,
                open[0].Number,
                config.Limit);
        }

        var number = await _configService.NextNumberAsync(guild.Id);

        try
        {
            var created = await guild.CreateTextChannelAsync(
                $"ticket-{number:D4}",
                properties =>
                {
                    properties.CategoryId = categoryId;
                    properties.Topic =
                        $"Ticket #{number:D4} • opened by {member.Username} ({member.Id})";

                    // Private from birth: the overwrites go in with the create
                    // call, so there is no public window afterwards.
                    properties.PermissionOverwrites =
                        BuildTicketOverwrites(guild, member, config);
                },
                new RequestOptions
                {
                    AuditLogReason = $"Ticket #{number:D4} opened by {member.Username}"
                });

            var ticket = new Ticket
            {
                GuildId = guild.Id,
                ChannelId = created.Id,
                OwnerId = member.Id,
                Number = number,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            await CommitTicketAsync(ticket);

            var message = (config.OpenMessage ?? DefaultOpenMessage)
                .Replace("{user}", member.Mention)
                .Replace("{username}", member.DisplayName)
                .Replace("{server}", guild.Name)
                .Replace("{ticket}", $"#{number:D4}");

            // Scoped ping so the opener actually gets a notification for their
            // own ticket. `AllowedTypes` has to be set explicitly: left unset the
            // payload carries no `parse` field and the whitelist is never applied.
            await created.SendMessageAsync(
                allowedMentions: new AllowedMentions
                {
                    AllowedTypes = AllowedMentionTypes.None,
                    UserIds = new List<ulong> { member.Id }
                },
                components: _builder.BuildOpening(ticket, member, message, reason));

            return new TicketOpenOutcome(
                TicketOpenResult.Opened,
                created.Id,
                number,
                config.Limit);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Ticket Open Error] {exception.Message}");

            return new TicketOpenOutcome(
                TicketOpenResult.CreateFailed,
                null,
                number,
                config.Limit);
        }
    }

    /// <summary>
    /// One-shot setup: reuses or creates a private "Tickets" category and a
    /// private "ticket-logs" channel, then switches tickets on. Channels the
    /// guild already made are reused untouched — only freshly created ones are
    /// locked down.
    /// </summary>
    public async Task<TicketSetupOutcome> SetupAsync(SocketGuild guild)
    {
        var bot = guild.CurrentUser;

        if (!(bot.GuildPermissions.ManageChannels ||
              bot.GuildPermissions.Administrator))
        {
            return new TicketSetupOutcome(
                TicketSetupResult.MissingBotPermission,
                null,
                null,
                false);
        }

        try
        {
            var config = _configService.GetConfig(guild.Id);

            var categoryId = ResolveExistingCategoryId(guild, config)
                             ?? await CreateCategoryAsync(guild);

            if (categoryId is null)
                return new TicketSetupOutcome(TicketSetupResult.Failed, null, null, false);

            var logChannelId = ResolveExistingLogChannelId(guild, config)
                               ?? (await CreateLogChannelAsync(guild, categoryId.Value))?.Id;

            await _configService.SetCategoryAsync(guild.Id, categoryId);

            if (logChannelId is not null)
                await _configService.SetLogChannelAsync(guild.Id, logChannelId);

            var persisted = await _configService.SetEnabledAsync(guild.Id, true);

            return new TicketSetupOutcome(
                TicketSetupResult.Done,
                categoryId,
                logChannelId,
                persisted);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Ticket Setup Error] {exception.Message}");
            return new TicketSetupOutcome(TicketSetupResult.Failed, null, null, false);
        }
    }

    /// <summary>
    /// Locks the ticket instead of deleting it: the transcript is saved first
    /// while the history is still reachable, then the opener and everyone added
    /// lose access, the channel is renamed, and the reopen/delete card is posted.
    /// </summary>
    public async Task<TicketActionOutcome> CloseAsync(
        SocketGuildUser actor,
        SocketTextChannel channel,
        string? reason)
    {
        if (!_tickets.TryGetValue(channel.Id, out var existing))
            return Fail(TicketActionResult.NotATicket);

        var config = _configService.GetConfig(channel.Guild.Id);

        // The opener may always close their own ticket, staff may close any.
        if (!(CanManage(actor, config) || actor.Id == existing.OwnerId))
            return Fail(TicketActionResult.MissingAccess);

        if (existing.IsClosed)
            return new TicketActionOutcome(TicketActionResult.AlreadyClosed, existing, null);

        var transcriptSaved = await PostTranscriptAsync(
            channel,
            existing,
            actor,
            reason);

        var ticket = existing.Clone();
        ticket.IsClosed = true;

        try
        {
            foreach (var userId in ticket.AddedUserIds.Append(ticket.OwnerId))
            {
                if (channel.Guild.GetUser(userId) is { } member)
                    await channel.RemovePermissionOverwriteAsync(member);
            }

            await channel.ModifyAsync(
                properties => properties.Name = $"closed-{ticket.Number:D4}",
                new RequestOptions
                {
                    AuditLogReason = $"Ticket closed by {actor.Username}"
                });

            await CommitTicketAsync(ticket);

            await channel.SendMessageAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildClosed(
                    ticket,
                    actor,
                    reason,
                    transcriptSaved,
                    config.LogChannelId is not null));

            return new TicketActionOutcome(TicketActionResult.Done, ticket, null);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Ticket Close Error] {exception.Message}");
            return new TicketActionOutcome(TicketActionResult.Failed, existing, null);
        }
    }

    public async Task<TicketActionOutcome> ReopenAsync(
        SocketGuildUser actor,
        SocketTextChannel channel)
    {
        if (!_tickets.TryGetValue(channel.Id, out var existing))
            return Fail(TicketActionResult.NotATicket);

        var config = _configService.GetConfig(channel.Guild.Id);

        if (!CanManage(actor, config))
            return Fail(TicketActionResult.MissingAccess);

        if (!existing.IsClosed)
            return new TicketActionOutcome(TicketActionResult.NotClosed, existing, null);

        var ticket = existing.Clone();
        ticket.IsClosed = false;

        try
        {
            foreach (var userId in ticket.AddedUserIds.Append(ticket.OwnerId))
            {
                if (channel.Guild.GetUser(userId) is { } member)
                {
                    await channel.AddPermissionOverwriteAsync(
                        member,
                        MemberPermissions);
                }
            }

            await channel.ModifyAsync(
                properties => properties.Name = $"ticket-{ticket.Number:D4}",
                new RequestOptions
                {
                    AuditLogReason = $"Ticket reopened by {actor.Username}"
                });

            await CommitTicketAsync(ticket);

            return new TicketActionOutcome(TicketActionResult.Done, ticket, null);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Ticket Reopen Error] {exception.Message}");
            return new TicketActionOutcome(TicketActionResult.Failed, existing, null);
        }
    }

    public async Task<TicketActionOutcome> DeleteAsync(
        SocketGuildUser actor,
        SocketTextChannel channel)
    {
        if (!_tickets.TryGetValue(channel.Id, out var ticket))
            return Fail(TicketActionResult.NotATicket);

        if (!CanManage(actor, _configService.GetConfig(channel.Guild.Id)))
            return Fail(TicketActionResult.MissingAccess);

        try
        {
            await channel.DeleteAsync(new RequestOptions
            {
                AuditLogReason = $"Ticket deleted by {actor.Username}"
            });

            _tickets.TryRemove(channel.Id, out _);
            await DeleteDocumentAsync(channel.Id);

            return new TicketActionOutcome(TicketActionResult.Done, ticket, null);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Ticket Delete Error] {exception.Message}");
            return new TicketActionOutcome(TicketActionResult.Failed, ticket, null);
        }
    }

    public async Task<TicketActionOutcome> SetClaimAsync(
        SocketGuildUser actor,
        SocketTextChannel channel,
        bool claim)
    {
        if (!_tickets.TryGetValue(channel.Id, out var existing))
            return Fail(TicketActionResult.NotATicket);

        var config = _configService.GetConfig(channel.Guild.Id);

        if (!CanManage(actor, config))
            return Fail(TicketActionResult.MissingAccess);

        if (claim && existing.ClaimedBy is { } holder)
        {
            return new TicketActionOutcome(
                TicketActionResult.AlreadyClaimed,
                existing,
                holder.ToString());
        }

        // Anyone with support access can release a stuck ticket, not just the
        // staff member who took it.
        if (!claim && existing.ClaimedBy is null)
            return new TicketActionOutcome(TicketActionResult.NotClaimed, existing, null);

        var ticket = existing.Clone();
        ticket.ClaimedBy = claim ? actor.Id : null;
        await CommitTicketAsync(ticket);

        return new TicketActionOutcome(TicketActionResult.Done, ticket, null);
    }

    public async Task<TicketActionOutcome> SetMemberAsync(
        SocketGuildUser actor,
        SocketTextChannel channel,
        SocketGuildUser target,
        bool add)
    {
        if (!_tickets.TryGetValue(channel.Id, out var existing))
            return Fail(TicketActionResult.NotATicket);

        if (!CanManage(actor, _configService.GetConfig(channel.Guild.Id)))
            return Fail(TicketActionResult.MissingAccess);

        if (target.Id == existing.OwnerId)
            return new TicketActionOutcome(TicketActionResult.IsOwner, existing, null);

        if (add && existing.AddedUserIds.Contains(target.Id))
            return new TicketActionOutcome(TicketActionResult.AlreadyAdded, existing, null);

        if (!add && !existing.AddedUserIds.Contains(target.Id))
            return new TicketActionOutcome(TicketActionResult.NotAdded, existing, null);

        var ticket = existing.Clone();

        try
        {
            if (add)
            {
                await channel.AddPermissionOverwriteAsync(target, MemberPermissions);
                ticket.AddedUserIds.Add(target.Id);
            }
            else
            {
                await channel.RemovePermissionOverwriteAsync(target);
                ticket.AddedUserIds.Remove(target.Id);
            }

            await CommitTicketAsync(ticket);

            return new TicketActionOutcome(TicketActionResult.Done, ticket, null);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Ticket Member Error] {exception.Message}");
            return new TicketActionOutcome(TicketActionResult.Failed, existing, null);
        }
    }

    public async Task<TicketActionOutcome> RenameAsync(
        SocketGuildUser actor,
        SocketTextChannel channel,
        string name)
    {
        if (!_tickets.TryGetValue(channel.Id, out var ticket))
            return Fail(TicketActionResult.NotATicket);

        if (!CanManage(actor, _configService.GetConfig(channel.Guild.Id)))
            return Fail(TicketActionResult.MissingAccess);

        var slug = Slugify(name);

        if (slug.Length is < MinChannelNameLength or > MaxChannelNameLength)
            return new TicketActionOutcome(TicketActionResult.InvalidName, ticket, null);

        try
        {
            await channel.ModifyAsync(
                properties => properties.Name = slug,
                new RequestOptions
                {
                    AuditLogReason = $"Ticket renamed by {actor.Username}"
                });

            return new TicketActionOutcome(TicketActionResult.Done, ticket, slug);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Ticket Rename Error] {exception.Message}");
            return new TicketActionOutcome(TicketActionResult.Failed, ticket, null);
        }
    }

    /// <summary>Saves a transcript without closing, for the transcript command.</summary>
    public async Task<TicketActionOutcome> SaveTranscriptAsync(
        SocketGuildUser actor,
        SocketTextChannel channel)
    {
        if (!_tickets.TryGetValue(channel.Id, out var ticket))
            return Fail(TicketActionResult.NotATicket);

        var config = _configService.GetConfig(channel.Guild.Id);

        if (!CanManage(actor, config))
            return Fail(TicketActionResult.MissingAccess);

        if (config.LogChannelId is null)
            return new TicketActionOutcome(TicketActionResult.NoLogChannel, ticket, null);

        var saved = await PostTranscriptAsync(channel, ticket, actor, reason: null);

        return new TicketActionOutcome(
            saved ? TicketActionResult.Done : TicketActionResult.Failed,
            ticket,
            null);
    }

    /// <summary>
    /// Drops the record for a ticket channel deleted outside the bot, so the
    /// owner's allowance frees up and the list card stays truthful.
    /// </summary>
    public async Task HandleChannelDestroyedAsync(SocketChannel channel)
    {
        try
        {
            if (channel is not SocketGuildChannel guildChannel)
                return;

            if (!_tickets.TryRemove(guildChannel.Id, out _))
                return;

            await DeleteDocumentAsync(guildChannel.Id);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Ticket Cleanup Error] {exception.Message}");
        }
    }

    private static bool CanManage(SocketGuildUser member, TicketConfig config)
    {
        if (member.GuildPermissions.Administrator ||
            member.GuildPermissions.ManageChannels)
        {
            return true;
        }

        return config.HasSupportRole(member.Roles.Select(role => role.Id));
    }

    private static ulong? ResolveExistingCategoryId(
        SocketGuild guild,
        TicketConfig config)
    {
        if (config.CategoryId is { } configured &&
            guild.CategoryChannels.Any(category => category.Id == configured))
        {
            return configured;
        }

        return guild.CategoryChannels
            .FirstOrDefault(category =>
                category.Name.Equals("Tickets", StringComparison.OrdinalIgnoreCase))
            ?.Id;
    }

    private static async Task<ulong?> CreateCategoryAsync(SocketGuild guild)
    {
        var created = await guild.CreateCategoryChannelAsync(
            "Tickets",
            properties =>
            {
                properties.PermissionOverwrites = BuildPrivateOverwrites(guild);
            },
            new RequestOptions
            {
                AuditLogReason = "Tickets category for support ticket channels"
            });

        return created?.Id;
    }

    private static ulong? ResolveExistingLogChannelId(
        SocketGuild guild,
        TicketConfig config)
    {
        if (config.LogChannelId is { } configured &&
            guild.GetTextChannel(configured) is not null)
        {
            return configured;
        }

        return guild.TextChannels
            .FirstOrDefault(channel =>
                channel.Name.Equals("ticket-logs", StringComparison.OrdinalIgnoreCase))
            ?.Id;
    }

    private static async Task<ITextChannel?> CreateLogChannelAsync(
        SocketGuild guild,
        ulong categoryId)
    {
        return await guild.CreateTextChannelAsync(
            "ticket-logs",
            properties =>
            {
                properties.CategoryId = categoryId;
                properties.Topic = "Ticket transcripts and close summaries";
                properties.PermissionOverwrites = BuildPrivateOverwrites(guild);
            },
            new RequestOptions
            {
                AuditLogReason = "Ticket transcript log channel"
            });
    }

    private static Overwrite[] BuildPrivateOverwrites(SocketGuild guild)
    {
        return new[]
        {
            new Overwrite(
                guild.EveryoneRole.Id,
                PermissionTarget.Role,
                new OverwritePermissions(viewChannel: PermValue.Deny)),
            new Overwrite(guild.CurrentUser.Id, PermissionTarget.User, BotPermissions)
        };
    }

    private static Overwrite[] BuildTicketOverwrites(
        SocketGuild guild,
        SocketGuildUser owner,
        TicketConfig config)
    {
        var overwrites = new List<Overwrite>
        {
            new(
                guild.EveryoneRole.Id,
                PermissionTarget.Role,
                new OverwritePermissions(viewChannel: PermValue.Deny)),
            new(guild.CurrentUser.Id, PermissionTarget.User, BotPermissions),
            new(owner.Id, PermissionTarget.User, MemberPermissions)
        };

        foreach (var roleId in config.SupportRoleIds)
        {
            // A deleted support role would make Discord reject the whole create
            // call, so stale ids are skipped rather than passed through.
            if (guild.GetRole(roleId) is not null)
            {
                overwrites.Add(new Overwrite(
                    roleId,
                    PermissionTarget.Role,
                    SupportPermissions));
            }
        }

        return overwrites.ToArray();
    }

    private async Task<bool> PostTranscriptAsync(
        SocketTextChannel channel,
        Ticket ticket,
        SocketGuildUser actor,
        string? reason)
    {
        var config = _configService.GetConfig(channel.Guild.Id);

        if (config.LogChannelId is not { } logChannelId)
            return false;

        var logChannel = channel.Guild.GetTextChannel(logChannelId);

        if (logChannel is null)
            return false;

        try
        {
            var messages = await channel
                .GetMessagesAsync(MaxTranscriptMessages)
                .FlattenAsync();

            var ordered = messages
                .OrderBy(message => message.CreatedAt)
                .ToList();

            var body = new StringBuilder();

            body.AppendLine($"Ticket #{ticket.Number:D4} — {channel.Guild.Name}");
            body.AppendLine($"Channel   : #{channel.Name} ({channel.Id})");
            body.AppendLine($"Opened by : {ticket.OwnerId}");
            body.AppendLine($"Closed by : {actor.Username} ({actor.Id})");
            body.AppendLine(
                $"Opened at : {DateTimeOffset.FromUnixTimeSeconds(ticket.CreatedAt).UtcDateTime:yyyy-MM-dd HH:mm:ss} UTC");
            body.AppendLine($"Messages  : {ordered.Count}");

            if (!string.IsNullOrWhiteSpace(reason))
                body.AppendLine($"Reason    : {reason}");

            body.AppendLine();

            // The fetch is capped, so say so instead of silently trimming the
            // start of a long conversation.
            if (ordered.Count >= MaxTranscriptMessages)
            {
                body.AppendLine(
                    $"[truncated — only the last {MaxTranscriptMessages} messages are included]");
                body.AppendLine();
            }

            foreach (var message in ordered)
            {
                var stamp = message.CreatedAt.UtcDateTime
                    .ToString("yyyy-MM-dd HH:mm:ss");

                var content = string.IsNullOrWhiteSpace(message.Content)
                    ? "[no text content]"
                    : message.Content.Replace("\r\n", "\n");

                body.AppendLine(
                    $"[{stamp} UTC] {message.Author.Username} ({message.Author.Id}): {content}");

                foreach (var attachment in message.Attachments)
                {
                    body.AppendLine(
                        $"    [attachment] {attachment.Filename} — {attachment.Url}");
                }
            }

            await logChannel.SendMessageAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildTranscriptLog(
                    ticket,
                    channel.Guild,
                    actor,
                    ordered.Count,
                    reason));

            // Sent as its own plain message: a Components V2 payload has to
            // reference an attachment through a file component, and the log card
            // above already carries the summary.
            using var stream = new MemoryStream(
                Encoding.UTF8.GetBytes(body.ToString()));

            await logChannel.SendFileAsync(
                stream,
                $"ticket-{ticket.Number:D4}.txt",
                allowedMentions: AllowedMentions.None);

            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Ticket Transcript Error] {exception.Message}");
            return false;
        }
    }

    private async Task PruneMissingAsync(SocketGuild guild)
    {
        var stale = _tickets.Values
            .Where(ticket =>
                ticket.GuildId == guild.Id &&
                guild.GetTextChannel(ticket.ChannelId) is null)
            .ToList();

        foreach (var ticket in stale)
        {
            _tickets.TryRemove(ticket.ChannelId, out _);
            await DeleteDocumentAsync(ticket.ChannelId);
        }
    }

    private async Task<bool> CommitTicketAsync(Ticket ticket)
    {
        _tickets[ticket.ChannelId] = ticket;

        if (_collection is null)
            return false;

        try
        {
            var document = ToDocument(ticket);

            var filter = Builders<TicketDocument>.Filter.Eq(
                existing => existing.Id,
                document.Id);

            await _collection.ReplaceOneAsync(
                filter,
                document,
                new ReplaceOptions { IsUpsert = true });

            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Ticket Save Error] {exception}");
            return false;
        }
    }

    private async Task DeleteDocumentAsync(ulong channelId)
    {
        if (_collection is null)
            return;

        try
        {
            var filter = Builders<TicketDocument>.Filter.Eq(
                existing => existing.Id,
                channelId.ToString());

            await _collection.DeleteOneAsync(filter);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Ticket Delete Error] {exception}");
        }
    }

    private static TicketActionOutcome Fail(TicketActionResult result)
    {
        return new TicketActionOutcome(result, null, null);
    }

    /// <summary>Turns free text into something Discord accepts as a channel name.</summary>
    private static string Slugify(string value)
    {
        var builder = new StringBuilder();

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character) || character is '-' or '_')
                builder.Append(character);
            else if (char.IsWhiteSpace(character))
                builder.Append('-');
        }

        return builder.ToString().Trim('-');
    }

    private static TicketDocument ToDocument(Ticket ticket)
    {
        return new TicketDocument
        {
            Id = ticket.ChannelId.ToString(),
            GuildId = ticket.GuildId.ToString(),
            OwnerId = ticket.OwnerId.ToString(),
            Number = ticket.Number,
            ClaimedBy = ticket.ClaimedBy?.ToString(),
            Closed = ticket.IsClosed,
            CreatedAt = ticket.CreatedAt,
            AddedUsers = ticket.AddedUserIds
                .Select(userId => userId.ToString())
                .ToList()
        };
    }

    private static Ticket? FromDocument(TicketDocument document)
    {
        if (!ulong.TryParse(document.Id, out var channelId) ||
            !ulong.TryParse(document.GuildId, out var guildId) ||
            !ulong.TryParse(document.OwnerId, out var ownerId))
        {
            return null;
        }

        var ticket = new Ticket
        {
            GuildId = guildId,
            ChannelId = channelId,
            OwnerId = ownerId,
            Number = document.Number,
            IsClosed = document.Closed,
            CreatedAt = document.CreatedAt
        };

        if (ulong.TryParse(document.ClaimedBy, out var claimedBy))
            ticket.ClaimedBy = claimedBy;

        foreach (var raw in document.AddedUsers)
        {
            if (ulong.TryParse(raw, out var userId))
                ticket.AddedUserIds.Add(userId);
        }

        return ticket;
    }
}

public enum TicketOpenResult
{
    Opened,
    Disabled,
    CategoryMissing,
    MissingBotPermission,
    LimitReached,
    CreateFailed
}

public enum TicketActionResult
{
    Done,
    NotATicket,
    MissingAccess,
    AlreadyClosed,
    NotClosed,
    AlreadyClaimed,
    NotClaimed,
    AlreadyAdded,
    NotAdded,
    IsOwner,
    InvalidName,
    NoLogChannel,
    Failed
}

public enum TicketSetupResult
{
    Done,
    MissingBotPermission,
    Failed
}

public readonly record struct TicketOpenOutcome(
    TicketOpenResult Result,
    ulong? ChannelId,
    int Number,
    int Limit);

public readonly record struct TicketSetupOutcome(
    TicketSetupResult Result,
    ulong? CategoryId,
    ulong? LogChannelId,
    bool Persisted);

public readonly record struct TicketActionOutcome(
    TicketActionResult Result,
    Ticket? Ticket,
    string? Detail);
