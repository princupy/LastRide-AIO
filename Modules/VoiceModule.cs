using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;

namespace LastRide.Modules;

[Name("Voice")]
public sealed class VoiceModule : ModuleBase<SocketCommandContext>
{
    private readonly VoiceComponentBuilder _builder;

    public VoiceModule(VoiceComponentBuilder builder)
    {
        _builder = builder;
    }

    // ----- Mute / Unmute -----

    [Command("vcmute")]
    [Alias("vmute")]
    [Summary("Server-mutes a member in their voice channel.")]
    public Task VcMuteAsync([Remainder] string? query = null) =>
        RunStateAsync(query, CanMute, "Mute Members", "Voice Muted",
            desired: true, user => user.IsMuted, "voice-muted",
            (props, value) => props.Mute = value);

    [Command("vcunmute")]
    [Alias("vunmute")]
    [Summary("Removes a member's server voice mute.")]
    public Task VcUnmuteAsync([Remainder] string? query = null) =>
        RunStateAsync(query, CanMute, "Mute Members", "Voice Unmuted",
            desired: false, user => user.IsMuted, "voice-muted",
            (props, value) => props.Mute = value);

    [Command("vcmuteall")]
    [Summary("Server-mutes everyone in your current voice channel.")]
    public Task VcMuteAllAsync() =>
        RunBulkStateAsync(CanMute, "Mute Members", "Voice Muted (All)",
            desired: true, user => user.IsMuted,
            (props, value) => props.Mute = value);

    [Command("vcunmuteall")]
    [Summary("Removes the server voice mute from everyone in your channel.")]
    public Task VcUnmuteAllAsync() =>
        RunBulkStateAsync(CanMute, "Mute Members", "Voice Unmuted (All)",
            desired: false, user => user.IsMuted,
            (props, value) => props.Mute = value);

    // ----- Deafen / Undeafen -----

    [Command("vcdeafen")]
    [Alias("vdeafen")]
    [Summary("Server-deafens a member in their voice channel.")]
    public Task VcDeafenAsync([Remainder] string? query = null) =>
        RunStateAsync(query, CanDeafen, "Deafen Members", "Voice Deafened",
            desired: true, user => user.IsDeafened, "voice-deafened",
            (props, value) => props.Deaf = value);

    [Command("vcundeafen")]
    [Alias("vundeafen")]
    [Summary("Removes a member's server voice deafen.")]
    public Task VcUndeafenAsync([Remainder] string? query = null) =>
        RunStateAsync(query, CanDeafen, "Deafen Members", "Voice Undeafened",
            desired: false, user => user.IsDeafened, "voice-deafened",
            (props, value) => props.Deaf = value);

    [Command("vcdeafenall")]
    [Summary("Server-deafens everyone in your current voice channel.")]
    public Task VcDeafenAllAsync() =>
        RunBulkStateAsync(CanDeafen, "Deafen Members", "Voice Deafened (All)",
            desired: true, user => user.IsDeafened,
            (props, value) => props.Deaf = value);

    [Command("vcundeafenall")]
    [Summary("Removes the server voice deafen from everyone in your channel.")]
    public Task VcUndeafenAllAsync() =>
        RunBulkStateAsync(CanDeafen, "Deafen Members", "Voice Undeafened (All)",
            desired: false, user => user.IsDeafened,
            (props, value) => props.Deaf = value);

    // ----- Kick / Disconnect -----

    [Command("vckick")]
    [Alias("vcdisconnect", "vcdc")]
    [Summary("Disconnects a member from their voice channel.")]
    public async Task VcKickAsync([Remainder] string? query = null)
    {
        var prepared = await PrepareTargetAsync(query, CanMove, "Move Members");

        if (prepared is null)
            return;

        var (moderator, target) = prepared.Value;
        var channelId = target.VoiceChannel!.Id;

        try
        {
            await target.ModifyAsync(
                props => props.Channel = null,
                Reason(moderator, "Voice disconnect"));

            await ReplyComponentsAsync(_builder.BuildActionResult(
                "Disconnected",
                target.Id,
                moderator.Id,
                $"**Channel:** <#{channelId}>"));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[VcKick Error] {exception}");

            await ReplyNoticeAsync(
                "Action Failed",
                "I could not disconnect this member. Check my permissions.");
        }
    }

    [Command("vckickall")]
    [Alias("vcdisconnectall", "vcdcall")]
    [Summary("Disconnects everyone from your current voice channel.")]
    public async Task VcKickAllAsync()
    {
        var moderator = await EnsureModAsync(CanMove, "Move Members");

        if (moderator is null)
            return;

        var channel = moderator.VoiceChannel;

        if (channel is null)
        {
            await ReplyNoticeAsync(
                "Not In Voice",
                "Join the voice channel you want to clear first.");
            return;
        }

        var botId = Context.Guild.CurrentUser.Id;
        var disconnected = 0;
        var failed = 0;

        foreach (var member in channel.ConnectedUsers.ToArray())
        {
            if (member.Id == botId || member.Id == moderator.Id)
                continue;

            try
            {
                await member.ModifyAsync(
                    props => props.Channel = null,
                    Reason(moderator, "Voice disconnect all"));

                disconnected++;
            }
            catch (Exception exception)
            {
                failed++;
                Console.WriteLine(
                    $"[VcKickAll Error] {member.Id}: {exception.Message}");
            }
        }

        await ReplyComponentsAsync(_builder.BuildBulkResult(
            "Disconnected (All)",
            disconnected,
            skipped: 0,
            failed,
            moderator.Id,
            channel.Name));
    }

    // ----- Move / Pull -----

    [Command("vcmove")]
    [Alias("vmove")]
    [Summary("Moves a member to a voice channel, or shows a channel picker.")]
    public async Task VcMoveAsync([Remainder] string? input = null)
    {
        var (userQuery, channelQuery) = SplitFirstWord((input ?? string.Empty).Trim());

        var prepared = await PrepareTargetAsync(userQuery, CanMove, "Move Members");

        if (prepared is null)
            return;

        var (moderator, target) = prepared.Value;

        if (channelQuery.Length == 0)
        {
            // No destination given — let the moderator pick one from a dropdown.
            await ReplyComponentsAsync(_builder.BuildMoveMenu(
                Context.Guild,
                "move",
                moderator.Id,
                target.Id,
                target.VoiceChannel!.Id));
            return;
        }

        var destination = ResolveVoiceChannel(channelQuery);

        if (destination is null)
        {
            await ReplyNoticeAsync(
                "Channel Not Found",
                "I could not find that voice channel. Mention it, or give its name or ID.");
            return;
        }

        if (target.VoiceChannel!.Id == destination.Id)
        {
            await ReplyNoticeAsync(
                "Already There",
                $"<@{target.Id}> is already in {Sanitize(destination.Name)}.");
            return;
        }

        try
        {
            await target.ModifyAsync(
                props => props.Channel = destination,
                Reason(moderator, "Voice move"));

            await ReplyComponentsAsync(_builder.BuildActionResult(
                "Member Moved",
                target.Id,
                moderator.Id,
                $"**Channel:** <#{destination.Id}>"));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[VcMove Error] {exception}");

            await ReplyNoticeAsync(
                "Action Failed",
                "I could not move this member. Check my permissions.");
        }
    }

    [Command("vcmoveall")]
    [Summary("Shows a picker, then moves everyone in your channel there.")]
    public async Task VcMoveAllAsync()
    {
        var moderator = await EnsureModAsync(CanMove, "Move Members");

        if (moderator is null)
            return;

        var source = moderator.VoiceChannel;

        if (source is null)
        {
            await ReplyNoticeAsync(
                "Not In Voice",
                "Join the voice channel whose members you want to move.");
            return;
        }

        await ReplyComponentsAsync(_builder.BuildMoveMenu(
            Context.Guild,
            "moveall",
            moderator.Id,
            targetId: 0,
            excludeChannelId: source.Id));
    }

    [Command("vcpull")]
    [Alias("vpull")]
    [Summary("Pulls a member from their voice channel into yours.")]
    public async Task VcPullAsync([Remainder] string? query = null)
    {
        var prepared = await PrepareTargetAsync(query, CanMove, "Move Members");

        if (prepared is null)
            return;

        var (moderator, target) = prepared.Value;
        var destination = moderator.VoiceChannel;

        if (destination is null)
        {
            await ReplyNoticeAsync(
                "Not In Voice",
                "Join a voice channel first so I can pull the member to you.");
            return;
        }

        if (target.VoiceChannel!.Id == destination.Id)
        {
            await ReplyNoticeAsync(
                "Already Here",
                $"<@{target.Id}> is already in your voice channel.");
            return;
        }

        try
        {
            await target.ModifyAsync(
                props => props.Channel = destination,
                Reason(moderator, "Voice pull"));

            await ReplyComponentsAsync(_builder.BuildActionResult(
                "Member Pulled",
                target.Id,
                moderator.Id,
                $"**Channel:** <#{destination.Id}>"));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[VcPull Error] {exception}");

            await ReplyNoticeAsync(
                "Action Failed",
                "I could not pull this member. Check my permissions.");
        }
    }

    [Command("vcpullall")]
    [Summary("Shows a picker, then pulls everyone from that channel to yours.")]
    public async Task VcPullAllAsync()
    {
        var moderator = await EnsureModAsync(CanMove, "Move Members");

        if (moderator is null)
            return;

        var destination = moderator.VoiceChannel;

        if (destination is null)
        {
            await ReplyNoticeAsync(
                "Not In Voice",
                "Join a voice channel first so I can pull members to you.");
            return;
        }

        await ReplyComponentsAsync(_builder.BuildMoveMenu(
            Context.Guild,
            "pullall",
            moderator.Id,
            targetId: 0,
            excludeChannelId: destination.Id));
    }

    // ----- Lock / Unlock / Hide / Unhide -----

    [Command("vclock")]
    [Summary("Stops members from joining your current voice channel.")]
    public Task VcLockAsync() =>
        RunChannelOverwriteAsync(
            deny: true,
            useConnect: true,
            "Voice Locked",
            "This voice channel is already locked.");

    [Command("vcunlock")]
    [Summary("Lets members join your current voice channel again.")]
    public Task VcUnlockAsync() =>
        RunChannelOverwriteAsync(
            deny: false,
            useConnect: true,
            "Voice Unlocked",
            "This voice channel is not locked.");

    [Command("vchide")]
    [Summary("Hides your current voice channel from everyone.")]
    public Task VcHideAsync() =>
        RunChannelOverwriteAsync(
            deny: true,
            useConnect: false,
            "Voice Hidden",
            "This voice channel is already hidden.");

    [Command("vcunhide")]
    [Summary("Makes your current voice channel visible again.")]
    public Task VcUnhideAsync() =>
        RunChannelOverwriteAsync(
            deny: false,
            useConnect: false,
            "Voice Unhidden",
            "This voice channel is not hidden.");

    // ----- List -----

    [Command("vclist")]
    [Alias("vcmembers", "vcinvc")]
    [Summary("Lists everyone in your current voice channel with their state.")]
    public async Task VcListAsync()
    {
        if (Context.Guild is null)
        {
            await ReplyNoticeAsync(
                "Server Only",
                "This command can only be used in a server.");
            return;
        }

        var channel = (Context.User as SocketGuildUser)?.VoiceChannel;

        if (channel is null)
        {
            await ReplyNoticeAsync(
                "Not In Voice",
                "Join a voice channel to list its members.");
            return;
        }

        await ReplyComponentsAsync(_builder.BuildList(channel, Context.User.Id));
    }

    // ----- Shared runners -----

    private async Task RunStateAsync(
        string? query,
        Func<GuildPermissions, bool> hasPermission,
        string permissionLabel,
        string title,
        bool desired,
        Func<SocketGuildUser, bool> currentState,
        string stateNoun,
        Action<GuildUserProperties, bool> apply)
    {
        var prepared = await PrepareTargetAsync(query, hasPermission, permissionLabel);

        if (prepared is null)
            return;

        var (moderator, target) = prepared.Value;

        if (currentState(target) == desired)
        {
            await ReplyNoticeAsync(
                "No Change Needed",
                desired
                    ? $"<@{target.Id}> is already {stateNoun}."
                    : $"<@{target.Id}> is not {stateNoun}.");
            return;
        }

        try
        {
            await target.ModifyAsync(
                props => apply(props, desired),
                Reason(moderator, title));

            await ReplyComponentsAsync(_builder.BuildActionResult(
                title,
                target.Id,
                moderator.Id,
                $"**Channel:** <#{target.VoiceChannel!.Id}>"));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Voice {title} Error] {exception}");

            await ReplyNoticeAsync(
                "Action Failed",
                "I could not update this member. Check my permissions.");
        }
    }

    private async Task RunBulkStateAsync(
        Func<GuildPermissions, bool> hasPermission,
        string permissionLabel,
        string title,
        bool desired,
        Func<SocketGuildUser, bool> currentState,
        Action<GuildUserProperties, bool> apply)
    {
        var moderator = await EnsureModAsync(hasPermission, permissionLabel);

        if (moderator is null)
            return;

        var channel = moderator.VoiceChannel;

        if (channel is null)
        {
            await ReplyNoticeAsync(
                "Not In Voice",
                "Join a voice channel first to use this command.");
            return;
        }

        var botId = Context.Guild.CurrentUser.Id;
        var affected = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var member in channel.ConnectedUsers.ToArray())
        {
            // The bot and the moderator running the command are never affected.
            if (member.Id == botId || member.Id == moderator.Id)
                continue;

            if (currentState(member) == desired)
            {
                skipped++;
                continue;
            }

            try
            {
                await member.ModifyAsync(
                    props => apply(props, desired),
                    Reason(moderator, title));

                affected++;
            }
            catch (Exception exception)
            {
                failed++;
                Console.WriteLine(
                    $"[Voice {title} Error] {member.Id}: {exception.Message}");
            }
        }

        await ReplyComponentsAsync(_builder.BuildBulkResult(
            title,
            affected,
            skipped,
            failed,
            moderator.Id,
            channel.Name));
    }

    private async Task RunChannelOverwriteAsync(
        bool deny,
        bool useConnect,
        string title,
        string alreadyMessage)
    {
        if (Context.Guild is null)
        {
            await ReplyNoticeAsync(
                "Server Only",
                "This command can only be used in a server.");
            return;
        }

        var moderator = Context.User as SocketGuildUser;

        if (moderator is null || !CanManageChannels(moderator.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "You need `Manage Channels` or `Administrator` permission to use this command.");
            return;
        }

        if (!CanEditPermissions(Context.Guild.CurrentUser.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Bot Permission",
                "I need `Manage Roles` or `Administrator` permission to edit channel permissions.");
            return;
        }

        var channel = moderator.VoiceChannel;

        if (channel is null)
        {
            await ReplyNoticeAsync(
                "Not In Voice",
                "Join the voice channel you want to update first.");
            return;
        }

        var everyoneRole = Context.Guild.EveryoneRole;
        var currentOverwrite = channel.GetPermissionOverwrite(everyoneRole);
        var currentValue = useConnect
            ? currentOverwrite?.Connect
            : currentOverwrite?.ViewChannel;
        var targetValue = deny ? PermValue.Deny : PermValue.Inherit;

        if (currentValue == targetValue)
        {
            await ReplyNoticeAsync("No Change Needed", alreadyMessage);
            return;
        }

        try
        {
            var overwrite = currentOverwrite ?? OverwritePermissions.InheritAll;
            var modified = useConnect
                ? overwrite.Modify(connect: targetValue)
                : overwrite.Modify(viewChannel: targetValue);

            await channel.AddPermissionOverwriteAsync(
                everyoneRole,
                modified,
                Reason(moderator, title));

            await ReplyComponentsAsync(_builder.BuildChannelResult(
                title,
                channel.Id,
                moderator.Id));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Voice {title} Error] {exception}");

            await ReplyNoticeAsync(
                "Action Failed",
                "I could not update this channel. Check my permissions and role position.");
        }
    }

    // ----- Helpers -----

    // Runs the guild / permission / target-resolution checks shared by every
    // single-target command and returns the moderator + a target that is
    // confirmed to be connected to a voice channel.
    private async Task<(SocketGuildUser Moderator, SocketGuildUser Target)?> PrepareTargetAsync(
        string? query,
        Func<GuildPermissions, bool> hasPermission,
        string permissionLabel)
    {
        var moderator = await EnsureModAsync(hasPermission, permissionLabel);

        if (moderator is null)
            return null;

        if (string.IsNullOrWhiteSpace(query))
        {
            await ReplyNoticeAsync(
                "Invalid Usage",
                "Mention a member or provide a valid user ID.");
            return null;
        }

        var target = ResolveTarget(query.Trim());

        if (target is null)
        {
            await ReplyNoticeAsync(
                "User Not Found",
                "I could not find that member. Mention them or provide a valid user ID.");
            return null;
        }

        var error = ValidateTarget(Context.Guild, moderator, target);

        if (error is not null)
        {
            await ReplyNoticeAsync("Action Failed", error);
            return null;
        }

        if (target.VoiceChannel is null)
        {
            await ReplyNoticeAsync(
                "Not In Voice",
                $"<@{target.Id}> is not connected to a voice channel.");
            return null;
        }

        return (moderator, target);
    }

    private async Task<SocketGuildUser?> EnsureModAsync(
        Func<GuildPermissions, bool> hasPermission,
        string permissionLabel)
    {
        if (Context.Guild is null)
        {
            await ReplyNoticeAsync(
                "Server Only",
                "This command can only be used in a server.");
            return null;
        }

        var moderator = Context.User as SocketGuildUser;

        if (moderator is null || !hasPermission(moderator.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                $"You need `{permissionLabel}` or `Administrator` permission to use this command.");
            return null;
        }

        if (!hasPermission(Context.Guild.CurrentUser.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Bot Permission",
                $"I need `{permissionLabel}` or `Administrator` permission to do that.");
            return null;
        }

        return moderator;
    }

    private static string? ValidateTarget(
        SocketGuild guild,
        SocketGuildUser moderator,
        SocketGuildUser target)
    {
        if (target.Id == moderator.Id)
            return "You cannot target yourself with this command.";

        if (target.Id == guild.CurrentUser.Id)
            return "I cannot target myself.";

        if (target.Id == guild.OwnerId)
            return "The server owner cannot be targeted.";

        return null;
    }

    private SocketGuildUser? ResolveTarget(string query)
    {
        if (MentionUtils.TryParseUser(query, out var userId) ||
            ulong.TryParse(query, out userId))
        {
            return Context.Guild.GetUser(userId);
        }

        var guildUser = Context.Guild.Users.FirstOrDefault(user =>
            user.Username.Equals(query, StringComparison.OrdinalIgnoreCase) ||
            user.DisplayName.Equals(query, StringComparison.OrdinalIgnoreCase));

        guildUser ??= Context.Guild.Users.FirstOrDefault(user =>
            user.Username.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            user.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase));

        return guildUser;
    }

    private SocketVoiceChannel? ResolveVoiceChannel(string query)
    {
        if (MentionUtils.TryParseChannel(query, out var channelId) ||
            ulong.TryParse(query, out channelId))
        {
            return Context.Guild.GetVoiceChannel(channelId);
        }

        return Context.Guild.VoiceChannels.FirstOrDefault(channel =>
                channel.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
            ?? Context.Guild.VoiceChannels.FirstOrDefault(channel =>
                channel.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static (string User, string Remainder) SplitFirstWord(string input)
    {
        var spaceIndex = input.IndexOf(' ');

        return spaceIndex < 0
            ? (input, string.Empty)
            : (input[..spaceIndex], input[(spaceIndex + 1)..].Trim());
    }

    private static RequestOptions Reason(SocketGuildUser moderator, string action)
    {
        return new RequestOptions
        {
            AuditLogReason = $"{action} by {moderator.Username}"
        };
    }

    private static string Sanitize(string value)
    {
        return value.Replace("`", "'");
    }

    private static bool CanMute(GuildPermissions permissions)
    {
        return permissions.MuteMembers || permissions.Administrator;
    }

    private static bool CanDeafen(GuildPermissions permissions)
    {
        return permissions.DeafenMembers || permissions.Administrator;
    }

    private static bool CanMove(GuildPermissions permissions)
    {
        return permissions.MoveMembers || permissions.Administrator;
    }

    private static bool CanManageChannels(GuildPermissions permissions)
    {
        return permissions.ManageChannels || permissions.Administrator;
    }

    private static bool CanEditPermissions(GuildPermissions permissions)
    {
        return permissions.ManageRoles || permissions.Administrator;
    }

    private Task ReplyComponentsAsync(MessageComponent components)
    {
        return ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: components);
    }

    private Task ReplyNoticeAsync(string title, string message)
    {
        return ReplyComponentsAsync(_builder.BuildNotice(title, message));
    }
}
