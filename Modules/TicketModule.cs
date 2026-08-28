using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Core;
using LastRide.Services;

namespace LastRide.Modules;

[Name("Ticket")]
public sealed class TicketModule : ModuleBase<SocketCommandContext>
{
    private readonly TicketConfigService _configService;
    private readonly TicketService _ticketService;
    private readonly TicketComponentBuilder _builder;
    private readonly PrefixService _prefixService;

    public TicketModule(
        TicketConfigService configService,
        TicketService ticketService,
        TicketComponentBuilder builder,
        PrefixService prefixService)
    {
        _configService = configService;
        _ticketService = ticketService;
        _builder = builder;
        _prefixService = prefixService;
    }

    [Command("ticket")]
    [Alias("tickets")]
    [Summary("Turn the ticket system on or off, view settings, or reset them.")]
    public async Task TicketAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);

        if (parts.Length == 0)
        {
            await ReplyStatusAsync(note: null);
            return;
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "status":
            case "config":
                await ReplyStatusAsync(note: null);
                return;

            case "on":
            case "enable":
                await SetEnabledAsync(true);
                return;

            case "off":
            case "disable":
                await SetEnabledAsync(false);
                return;

            case "list":
            case "open":
                await ReplyOpenListAsync();
                return;

            case "reset":
                await _configService.ResetAsync(Context.Guild.Id);
                await ReplyStatusAsync("Ticket configuration reset.");
                return;

            default:
                await ReplyNoticeAsync(
                    "Invalid Usage",
                    $"Usage: `{Prefix}ticket on/off`, `{Prefix}ticket status`, " +
                    $"`{Prefix}ticket list`, `{Prefix}ticket reset`.");
                return;
        }
    }

    [Command("ticketsetup")]
    [Alias("ticketinit")]
    [Summary("Create the ticket category and log channel, then enable tickets.")]
    public async Task TicketSetupAsync()
    {
        if (!await EnsureAllowedAsync())
            return;

        var outcome = await _ticketService.SetupAsync(Context.Guild);

        if (outcome.Result == TicketSetupResult.MissingBotPermission)
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "I need `Manage Channels` or `Administrator` to create the ticket " +
                "category and log channel.");

            return;
        }

        if (outcome.Result == TicketSetupResult.TwoFactorRequired)
        {
            await ReplyNoticeAsync(
                "Two-Factor Required",
                DiscordFailure.TwoFactorNotice());

            return;
        }

        if (outcome.Result != TicketSetupResult.Done)
        {
            await ReplyNoticeAsync(
                "Setup Failed",
                "I could not finish the setup. Check my permissions and whether the " +
                "server is at its channel limit, then try again.");

            return;
        }

        var lines = new List<string>
        {
            $"**Category:** <#{outcome.CategoryId}>",
            outcome.LogChannelId is { } logChannelId
                ? $"**Log Channel:** <#{logChannelId}>"
                : "**Log Channel:** `Not set` — transcripts will be skipped.",
            "**Status:** Enabled",
            $"Next: `{Prefix}ticketrole add @Support` then " +
            $"`{Prefix}ticketpanel #channel`."
        };

        await ReplyComponentsAsync(_builder.BuildActionCard(
            "Ticket Setup Complete",
            lines.ToArray()));

        if (!_configService.IsPersistent)
        {
            await ReplyNoticeAsync(
                "Not Saved",
                "The database is unavailable, so these settings will reset when I " +
                "restart.");
        }
    }

    [Command("ticketcategory")]
    [Summary("Set the category new ticket channels are created under.")]
    public async Task TicketCategoryAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);

        if (parts.Length == 0)
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}ticketcategory set <id|name>` to pick the category, or " +
                $"`{Prefix}ticketcategory remove` to clear it.");

            return;
        }

        var action = parts[0].ToLowerInvariant();

        if (action is "remove" or "clear" or "none" or "reset")
        {
            var cleared = await _configService.SetCategoryAsync(Context.Guild.Id, null);

            await ReplyResultAsync(
                "Ticket Category Cleared",
                "No tickets can be opened until a category is set again.",
                cleared);

            return;
        }

        // `set` is optional, so `ticketcategory set Support` and
        // `ticketcategory Support` resolve the same category.
        var query = action is "set" or "category"
            ? string.Join(' ', parts.Skip(1))
            : input!.Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            await ReplyNoticeAsync("Usage", $"`{Prefix}ticketcategory set <id|name>`");
            return;
        }

        if (!TryResolveCategory(query, out var category))
        {
            await ReplyNoticeAsync(
                "Category Not Found",
                $"I could not find a category matching {Inline(query)}. Pass its ID " +
                "or exact name.");

            return;
        }

        var persisted = await _configService.SetCategoryAsync(
            Context.Guild.Id,
            category.Id);

        var enabled = _configService.GetConfig(Context.Guild.Id).Enabled;

        await ReplyResultAsync(
            "Ticket Category Set",
            $"New tickets will be created under {Inline(category.Name)}." +
            (enabled ? string.Empty : $" Turn tickets on with `{Prefix}ticket on`."),
            persisted);
    }

    [Command("ticketlogs")]
    [Alias("ticketlogchannel")]
    [Summary("Set the channel transcripts and close summaries are posted to.")]
    public async Task TicketLogsAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);

        if (parts.Length == 0)
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}ticketlogs set #channel` to pick the log channel, or " +
                $"`{Prefix}ticketlogs remove` to clear it.");

            return;
        }

        var action = parts[0].ToLowerInvariant();

        if (action is "remove" or "clear" or "none" or "off" or "reset")
        {
            var cleared = await _configService.SetLogChannelAsync(Context.Guild.Id, null);

            await ReplyResultAsync(
                "Ticket Log Channel Cleared",
                "Transcripts will no longer be saved when a ticket is closed.",
                cleared);

            return;
        }

        var token = parts[0];

        if (action is "set" or "channel")
        {
            if (parts.Length < 2)
            {
                await ReplyNoticeAsync("Usage", $"`{Prefix}ticketlogs set #channel`");
                return;
            }

            token = parts[1];
        }

        if (!TryResolveTextChannel(token, out var channel))
        {
            await ReplyNoticeAsync(
                "Channel Not Found",
                "Mention a text channel in this server or pass its ID.");

            return;
        }

        var persisted = await _configService.SetLogChannelAsync(
            Context.Guild.Id,
            channel.Id);

        await ReplyResultAsync(
            "Ticket Log Channel Set",
            $"Transcripts and close summaries will be posted in {channel.Mention}.",
            persisted);
    }

    [Command("ticketrole")]
    [Alias("ticketsupport")]
    [Summary("Add, remove, or list the roles that can manage tickets.")]
    public async Task TicketRoleAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);
        var action = parts.Length == 0 ? "list" : parts[0].ToLowerInvariant();

        if (action is "list" or "show")
        {
            await ReplyComponentsAsync(_builder.BuildSupportRoleList(
                _configService.GetConfig(Context.Guild.Id).SupportRoleIds,
                Context.Guild,
                Prefix,
                _configService.IsPersistent));

            return;
        }

        if (action is not ("add" or "remove"))
        {
            await ReplyNoticeAsync(
                "Invalid Usage",
                $"Usage: `{Prefix}ticketrole add @role`, " +
                $"`{Prefix}ticketrole remove @role`, or `{Prefix}ticketrole list`.");

            return;
        }

        var query = string.Join(' ', parts.Skip(1));

        if (string.IsNullOrWhiteSpace(query))
        {
            await ReplyNoticeAsync("Usage", $"`{Prefix}ticketrole {action} @role`");
            return;
        }

        if (!TryResolveRole(query, out var role))
        {
            await ReplyNoticeAsync(
                "Role Not Found",
                $"I could not find a role matching {Inline(query)}.");

            return;
        }

        if (role.Id == Context.Guild.EveryoneRole.Id)
        {
            await ReplyNoticeAsync(
                "Invalid Role",
                "The `@everyone` role cannot be used as a support role — every " +
                "ticket would be public.");

            return;
        }

        var isAdd = action == "add";

        var update = isAdd
            ? await _configService.AddSupportRoleAsync(Context.Guild.Id, role.Id)
            : await _configService.RemoveSupportRoleAsync(Context.Guild.Id, role.Id);

        await (update.Result switch
        {
            TicketConfigResult.Added => ReplyResultAsync(
                "Support Role Added",
                $"<@&{role.Id}> can now see and manage tickets. Existing tickets " +
                "keep their current access.",
                update.Persisted),

            TicketConfigResult.Removed => ReplyResultAsync(
                "Support Role Removed",
                $"<@&{role.Id}> will not be added to new tickets any more.",
                update.Persisted),

            TicketConfigResult.AlreadyPresent => ReplyNoticeAsync(
                "Already Added",
                $"<@&{role.Id}> is already a support role."),

            TicketConfigResult.LimitReached => ReplyNoticeAsync(
                "Limit Reached",
                $"Only `{TicketConfigService.MaxSupportRoles}` support roles can be " +
                "configured. Remove one first."),

            _ => ReplyNoticeAsync(
                "Not Configured",
                $"<@&{role.Id}> is not a support role.")
        });
    }

    [Command("ticketmessage")]
    [Alias("ticketopenmessage")]
    [Summary("Set the message posted inside a freshly opened ticket.")]
    public async Task TicketMessageAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var trimmed = input?.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}ticketmessage <text>` — placeholders `{{user}}`, " +
                $"`{{username}}`, `{{server}}`, `{{ticket}}`. Use " +
                $"`{Prefix}ticketmessage reset` for the default.");

            return;
        }

        if (trimmed.ToLowerInvariant() is "reset" or "default" or "clear" or "none")
        {
            var cleared = await _configService.SetOpenMessageAsync(Context.Guild.Id, null);

            await ReplyResultAsync(
                "Ticket Message Reset",
                "The default opening message is back in use.",
                cleared);

            return;
        }

        if (trimmed.Length > TicketConfigService.MaxOpenMessageLength)
        {
            await ReplyNoticeAsync(
                "Message Too Long",
                $"Keep it under `{TicketConfigService.MaxOpenMessageLength}` characters.");

            return;
        }

        var persisted = await _configService.SetOpenMessageAsync(
            Context.Guild.Id,
            trimmed);

        await ReplyResultAsync(
            "Ticket Message Set",
            $"New opening message: {Inline(trimmed)}",
            persisted);
    }

    [Command("ticketpanel")]
    [Alias("ticketembed")]
    [Summary("Post the panel with the Create Ticket button members press.")]
    public async Task TicketPanelAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);
        var target = Context.Channel as SocketTextChannel;
        var skip = 0;

        if (parts.Length > 0 && TryResolveTextChannel(parts[0], out var mentioned))
        {
            target = mentioned;
            skip = 1;
        }

        if (target is null)
        {
            await ReplyNoticeAsync(
                "Channel Not Found",
                $"Mention the channel the panel should go in: " +
                $"`{Prefix}ticketpanel #support`.");

            return;
        }

        var text = string.Join(' ', parts.Skip(skip)).Trim();

        if (text.Length > TicketConfigService.MaxPanelMessageLength)
        {
            await ReplyNoticeAsync(
                "Message Too Long",
                $"Keep the panel text under " +
                $"`{TicketConfigService.MaxPanelMessageLength}` characters.");

            return;
        }

        // Passing text both saves it as the panel template and posts it, so the
        // same wording comes back the next time the panel is reposted.
        if (!string.IsNullOrWhiteSpace(text))
            await _configService.SetPanelMessageAsync(Context.Guild.Id, text);

        var config = _configService.GetConfig(Context.Guild.Id);

        try
        {
            await target.SendMessageAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildPanel(
                    Context.Guild,
                    _ticketService.RenderPanelMessage(Context.Guild)));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Ticket Panel Error] {DiscordFailure.Summarize(exception)}");

            await ReplyNoticeAsync(
                "Post Failed",
                $"I could not post in {target.Mention} — check that I have " +
                "`View Channel` and `Send Messages` there.");

            return;
        }

        var lines = new List<string>
        {
            $"**Channel:** {target.Mention}",
            "Any member can press **Create Ticket** to open a private channel."
        };

        if (!config.Enabled)
            lines.Add($"Tickets are still off — turn them on with `{Prefix}ticket on`.");

        if (config.CategoryId is null)
            lines.Add($"No category is set yet — run `{Prefix}ticketsetup`.");

        await ReplyComponentsAsync(_builder.BuildActionCard(
            "Ticket Panel Posted",
            lines.ToArray()));
    }

    [Command("ticketlimit")]
    [Summary("Set how many tickets one member may have open at the same time.")]
    public async Task TicketLimitAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);

        if (parts.Length == 0 || !int.TryParse(parts[0], out var limit))
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}ticketlimit <{TicketConfigService.MinLimit}-" +
                $"{TicketConfigService.MaxLimit}>` — currently " +
                $"`{_configService.GetConfig(Context.Guild.Id).Limit}`.");

            return;
        }

        if (limit < TicketConfigService.MinLimit || limit > TicketConfigService.MaxLimit)
        {
            await ReplyNoticeAsync(
                "Out Of Range",
                $"Pick a number between `{TicketConfigService.MinLimit}` and " +
                $"`{TicketConfigService.MaxLimit}`.");

            return;
        }

        var persisted = await _configService.SetLimitAsync(Context.Guild.Id, limit);

        await ReplyResultAsync(
            "Ticket Limit Set",
            $"Each member can now have `{limit}` ticket(s) open at once.",
            persisted);
    }

    [Command("ticketlist")]
    [Alias("activetickets")]
    [Summary("List every ticket that is currently open.")]
    public async Task TicketListAsync()
    {
        if (!await EnsureAllowedAsync())
            return;

        await ReplyOpenListAsync();
    }

    [Command("new")]
    [Alias("newticket", "createticket")]
    [Summary("Open a support ticket for yourself.")]
    public async Task NewTicketAsync([Remainder] string? reason = null)
    {
        if (!await EnsureGuildAsync())
            return;

        if (Context.User is not SocketGuildUser member)
            return;

        var trimmed = reason?.Trim();

        var outcome = await _ticketService.OpenAsync(
            member,
            string.IsNullOrWhiteSpace(trimmed) ? null : trimmed);

        await (outcome.Result switch
        {
            TicketOpenResult.Opened => ReplyComponentsAsync(_builder.BuildActionCard(
                "Ticket Created",
                $"**Channel:** <#{outcome.ChannelId}>",
                $"**Number:** `#{outcome.Number:D4}`",
                $"Close it with `{Prefix}close <reason>` when you are done.")),

            TicketOpenResult.Disabled => ReplyNoticeAsync(
                "Tickets Disabled",
                "The ticket system is switched off in this server."),

            TicketOpenResult.CategoryMissing => ReplyNoticeAsync(
                "Not Configured",
                "The ticket category is missing. Ask an admin to run " +
                $"`{Prefix}ticketsetup`."),

            TicketOpenResult.MissingBotPermission => ReplyNoticeAsync(
                "Missing Permission",
                "I need `Manage Channels` or `Administrator` to create ticket " +
                "channels."),

            TicketOpenResult.LimitReached => ReplyNoticeAsync(
                "Ticket Already Open",
                $"You can only have `{outcome.Limit}` ticket(s) open at once — " +
                $"yours is <#{outcome.ChannelId}>."),

            TicketOpenResult.TwoFactorRequired => ReplyNoticeAsync(
                "Two-Factor Required",
                DiscordFailure.TwoFactorNotice()),

            _ => ReplyNoticeAsync(
                "Could Not Open",
                "Creating the channel failed. Check my permissions in the ticket " +
                "category and try again.")
        });
    }

    [Command("close")]
    [Alias("ticketclose")]
    [Summary("Close the ticket in this channel and save its transcript.")]
    public async Task CloseAsync([Remainder] string? reason = null)
    {
        var channel = await ResolveTicketChannelAsync();

        if (channel is null)
            return;

        if (Context.User is not SocketGuildUser member)
            return;

        var trimmed = reason?.Trim();

        var outcome = await _ticketService.CloseAsync(
            member,
            channel,
            string.IsNullOrWhiteSpace(trimmed) ? null : trimmed);

        // The closed card posted inside the ticket is the confirmation, so a
        // success needs no second reply.
        if (Describe(outcome) is { } failure)
            await ReplyNoticeAsync("Close Failed", failure);
    }

    [Command("reopen")]
    [Alias("ticketreopen")]
    [Summary("Reopen a closed ticket and give its members access back.")]
    public async Task ReopenAsync()
    {
        var channel = await ResolveTicketChannelAsync();

        if (channel is null)
            return;

        if (Context.User is not SocketGuildUser member)
            return;

        var outcome = await _ticketService.ReopenAsync(member, channel);

        if (Describe(outcome) is { } failure)
        {
            await ReplyNoticeAsync("Reopen Failed", failure);
            return;
        }

        await ReplyComponentsAsync(_builder.BuildActionCard(
            "Ticket Reopened",
            $"**Reopened by:** {member.Mention}",
            "The member who opened it can see and post here again."));
    }

    [Command("ticketdelete")]
    [Alias("deleteticket")]
    [Summary("Delete this ticket channel for good.")]
    public async Task TicketDeleteAsync()
    {
        var channel = await ResolveTicketChannelAsync();

        if (channel is null)
            return;

        if (Context.User is not SocketGuildUser member)
            return;

        var outcome = await _ticketService.DeleteAsync(member, channel);

        // On success there is no channel left to reply in, so only failures talk.
        if (Describe(outcome) is { } failure)
            await ReplyNoticeAsync("Delete Failed", failure);
    }

    [Command("claim")]
    [Alias("ticketclaim")]
    [Summary("Claim this ticket so other staff know you are handling it.")]
    public async Task ClaimAsync()
    {
        await SetClaimAsync(claim: true);
    }

    [Command("unclaim")]
    [Alias("ticketunclaim")]
    [Summary("Release a claimed ticket so other staff can take it.")]
    public async Task UnclaimAsync()
    {
        await SetClaimAsync(claim: false);
    }

    [Command("ticketadd")]
    [Alias("adduser")]
    [Summary("Give another member access to this ticket.")]
    public async Task TicketAddAsync([Remainder] string? query = null)
    {
        await SetMemberAsync(query, add: true);
    }

    [Command("ticketremove")]
    [Alias("removeuser")]
    [Summary("Take away a member's access to this ticket.")]
    public async Task TicketRemoveAsync([Remainder] string? query = null)
    {
        await SetMemberAsync(query, add: false);
    }

    [Command("ticketrename")]
    [Alias("renameticket")]
    [Summary("Rename this ticket channel.")]
    public async Task TicketRenameAsync([Remainder] string? name = null)
    {
        var channel = await ResolveTicketChannelAsync();

        if (channel is null)
            return;

        if (Context.User is not SocketGuildUser member)
            return;

        var trimmed = name?.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            await ReplyNoticeAsync("Usage", $"`{Prefix}ticketrename <new name>`");
            return;
        }

        var outcome = await _ticketService.RenameAsync(member, channel, trimmed);

        if (Describe(outcome) is { } failure)
        {
            await ReplyNoticeAsync("Rename Failed", failure);
            return;
        }

        await ReplyResultAsync(
            "Ticket Renamed",
            $"This channel is now {Inline(outcome.Detail ?? trimmed)}.",
            _configService.IsPersistent);
    }

    [Command("transcript")]
    [Alias("tickettranscript")]
    [Summary("Save this ticket's transcript without closing it.")]
    public async Task TranscriptAsync()
    {
        var channel = await ResolveTicketChannelAsync();

        if (channel is null)
            return;

        if (Context.User is not SocketGuildUser member)
            return;

        var outcome = await _ticketService.SaveTranscriptAsync(member, channel);

        if (Describe(outcome) is { } failure)
        {
            await ReplyNoticeAsync("Transcript Failed", failure);
            return;
        }

        var logChannelId = _configService.GetConfig(Context.Guild.Id).LogChannelId;

        await ReplyComponentsAsync(_builder.BuildActionCard(
            "Transcript Saved",
            $"**Sent to:** <#{logChannelId}>",
            $"Up to `{TicketService.MaxTranscriptMessages}` of the most recent " +
            "messages are included."));
    }

    private async Task SetClaimAsync(bool claim)
    {
        var channel = await ResolveTicketChannelAsync();

        if (channel is null)
            return;

        if (Context.User is not SocketGuildUser member)
            return;

        var outcome = await _ticketService.SetClaimAsync(member, channel, claim);

        if (Describe(outcome) is { } failure)
        {
            await ReplyNoticeAsync(claim ? "Claim Failed" : "Unclaim Failed", failure);
            return;
        }

        await ReplyComponentsAsync(_builder.BuildActionCard(
            claim ? "Ticket Claimed" : "Ticket Released",
            claim
                ? $"{member.Mention} is handling this ticket."
                : "This ticket is unclaimed again — any staff member can take it."));
    }

    private async Task SetMemberAsync(string? query, bool add)
    {
        var channel = await ResolveTicketChannelAsync();

        if (channel is null)
            return;

        if (Context.User is not SocketGuildUser member)
            return;

        var trimmed = query?.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            await ReplyNoticeAsync(
                "Usage",
                add
                    ? $"`{Prefix}ticketadd @user`"
                    : $"`{Prefix}ticketremove @user`");

            return;
        }

        var target = ResolveTarget(trimmed);

        if (target is null)
        {
            await ReplyNoticeAsync(
                "Member Not Found",
                $"I could not find a member matching {Inline(trimmed)}. Mention them or " +
                "use their user ID.");

            return;
        }

        var outcome = await _ticketService.SetMemberAsync(member, channel, target, add);

        if (Describe(outcome) is { } failure)
        {
            await ReplyNoticeAsync(add ? "Add Failed" : "Remove Failed", failure);
            return;
        }

        // The added member is pinged so they notice the ticket; nobody else is.
        await ReplyComponentsAsync(
            _builder.BuildActionCard(
                add ? "Member Added" : "Member Removed",
                add
                    ? $"{target.Mention} can now see and post in this ticket."
                    : $"{target.Mention} no longer has access to this ticket."),
            add
                ? new AllowedMentions
                {
                    AllowedTypes = AllowedMentionTypes.None,
                    UserIds = new List<ulong> { target.Id }
                }
                : null);
    }

    private async Task SetEnabledAsync(bool enabled)
    {
        await _configService.SetEnabledAsync(Context.Guild.Id, enabled);

        await ReplyStatusAsync(enabled
            ? "Ticket system enabled."
            : "Ticket system disabled.");
    }

    private Task ReplyStatusAsync(string? note)
    {
        return ReplyComponentsAsync(_builder.BuildStatus(
            _configService.GetConfig(Context.Guild.Id),
            Context.Guild,
            Prefix,
            _configService.IsPersistent,
            note));
    }

    private Task ReplyOpenListAsync()
    {
        return ReplyComponentsAsync(_builder.BuildOpenList(
            _ticketService.GetOpenTickets(Context.Guild.Id),
            Context.Guild,
            Prefix));
    }

    /// <summary>
    /// Resolves the ticket the command was run in, replying with a notice when
    /// this channel is not one.
    /// </summary>
    private async Task<SocketTextChannel?> ResolveTicketChannelAsync()
    {
        if (!await EnsureGuildAsync())
            return null;

        if (Context.Channel is not SocketTextChannel channel ||
            _ticketService.GetTicket(channel.Id) is null)
        {
            await ReplyNoticeAsync(
                "Not A Ticket",
                "Run this inside a ticket channel.");

            return null;
        }

        return channel;
    }

    /// <summary>Failure text for an action outcome, or null when it succeeded.</summary>
    private string? Describe(TicketActionOutcome outcome)
    {
        return outcome.Result switch
        {
            TicketActionResult.Done => null,

            TicketActionResult.NotATicket => "This channel is not a ticket.",

            TicketActionResult.MissingAccess =>
                "You need a configured ticket support role, `Manage Channels`, or " +
                "`Administrator` for that.",

            TicketActionResult.AlreadyClosed => "This ticket is already closed.",

            TicketActionResult.NotClosed => "This ticket is not closed.",

            TicketActionResult.AlreadyClaimed => outcome.Detail is null
                ? "This ticket is already claimed."
                : $"This ticket is already claimed by <@{outcome.Detail}>.",

            TicketActionResult.NotClaimed => "Nobody has claimed this ticket yet.",

            TicketActionResult.AlreadyAdded =>
                "That member already has access to this ticket.",

            TicketActionResult.NotAdded =>
                "That member was never added to this ticket.",

            TicketActionResult.IsOwner =>
                "The member who opened the ticket cannot be added or removed.",

            TicketActionResult.InvalidName =>
                $"Use between `{TicketService.MinChannelNameLength}` and " +
                $"`{TicketService.MaxChannelNameLength}` letters, numbers, or dashes.",

            TicketActionResult.NoLogChannel =>
                "No ticket log channel is set, so there is nowhere to send the " +
                $"transcript. Set one with `{Prefix}ticketlogs set #channel`.",

            _ => "Something went wrong — check that I still have `Manage Channels` " +
                 "and can post here."
        };
    }

    private bool TryResolveCategory(string query, out SocketCategoryChannel category)
    {
        category = null!;

        if (string.IsNullOrWhiteSpace(query))
            return false;

        query = query.Trim();

        if (MentionUtils.TryParseChannel(query, out var channelId) ||
            ulong.TryParse(query, out channelId))
        {
            var byId = Context.Guild.CategoryChannels
                .FirstOrDefault(candidate => candidate.Id == channelId);

            if (byId is null)
                return false;

            category = byId;
            return true;
        }

        var byName = Context.Guild.CategoryChannels.FirstOrDefault(candidate =>
            candidate.Name.Equals(query, StringComparison.OrdinalIgnoreCase));

        if (byName is not null)
        {
            category = byName;
            return true;
        }

        // A partial name is only trusted when exactly one category can match it, so
        // "support" cannot silently pick "Support Archive" over "Support Tickets".
        var partialMatches = Context.Guild.CategoryChannels
            .Where(candidate => candidate.Name.Contains(
                query,
                StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();

        if (partialMatches.Length != 1)
            return false;

        category = partialMatches[0];
        return true;
    }

    private bool TryResolveTextChannel(string token, out SocketTextChannel channel)
    {
        channel = null!;

        if (!MentionUtils.TryParseChannel(token, out var channelId) &&
            !ulong.TryParse(token, out channelId))
        {
            return false;
        }

        var resolved = Context.Guild.GetTextChannel(channelId);

        if (resolved is null)
            return false;

        channel = resolved;
        return true;
    }

    private bool TryResolveRole(string query, out SocketRole role)
    {
        role = null!;

        if (string.IsNullOrWhiteSpace(query))
            return false;

        query = query.Trim();

        if (MentionUtils.TryParseRole(query, out var roleId) ||
            ulong.TryParse(query, out roleId))
        {
            var resolvedRole = Context.Guild.GetRole(roleId);

            if (resolvedRole is null)
                return false;

            role = resolvedRole;
            return true;
        }

        var exactRole = Context.Guild.Roles.FirstOrDefault(candidate =>
            candidate.Name.Equals(query, StringComparison.OrdinalIgnoreCase));

        if (exactRole is not null)
        {
            role = exactRole;
            return true;
        }

        // A partial name is only trusted when exactly one role can match it — see
        // AddRoleModule for the ordering problem this avoids.
        var partialMatches = Context.Guild.Roles
            .Where(candidate => candidate.Name.Contains(
                query,
                StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();

        if (partialMatches.Length != 1)
            return false;

        role = partialMatches[0];
        return true;
    }

    /// <summary>
    /// Only an explicit reference counts — see <see cref="UserReference"/> for why a plain
    /// name is refused rather than matched.
    /// </summary>
    private SocketGuildUser? ResolveTarget(string query)
    {
        return UserReference.TryParse(query, out var userId)
            ? Context.Guild.GetUser(userId)
            : null;
    }

    private async Task<bool> EnsureGuildAsync()
    {
        if (Context.Guild is not null)
            return true;

        await ReplyNoticeAsync("Server Only", "This command can only be used in a server.");
        return false;
    }

    private async Task<bool> EnsureAllowedAsync()
    {
        if (!await EnsureGuildAsync())
            return false;

        if (Context.User is not SocketGuildUser user ||
            !(user.GuildPermissions.ManageGuild || user.GuildPermissions.Administrator))
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "You need `Manage Server` or `Administrator` permission to manage " +
                "the ticket system.");

            return false;
        }

        return true;
    }

    private string Prefix => _prefixService.GetPrefix(Context.Guild?.Id);

    private static string[] Split(string? input)
    {
        return string.IsNullOrWhiteSpace(input)
            ? Array.Empty<string>()
            : input.Trim().Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private Task ReplyComponentsAsync(
        MessageComponent components,
        AllowedMentions? allowedMentions = null)
    {
        return ReplyAsync(
            allowedMentions: allowedMentions ?? AllowedMentions.None,
            components: components);
    }

    private Task ReplyResultAsync(string title, string message, bool persisted)
    {
        return ReplyComponentsAsync(_builder.BuildResult(title, message, persisted));
    }

    private Task ReplyNoticeAsync(string title, string message)
    {
        return ReplyComponentsAsync(_builder.BuildNotice(title, message));
    }

    private static string Inline(string value)
    {
        return $"`{value.Replace("`", "'")}`";
    }
}
