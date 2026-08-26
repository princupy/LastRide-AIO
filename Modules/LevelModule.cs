using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Models;
using LastRide.Services;

namespace LastRide.Modules;

[Name("Leveling")]
public sealed class LevelModule : ModuleBase<SocketCommandContext>
{
    private readonly LevelConfigService _configService;
    private readonly LevelService _levelService;
    private readonly LevelComponentBuilder _builder;
    private readonly PrefixService _prefixService;

    public LevelModule(
        LevelConfigService configService,
        LevelService levelService,
        LevelComponentBuilder builder,
        PrefixService prefixService)
    {
        _configService = configService;
        _levelService = levelService;
        _builder = builder;
        _prefixService = prefixService;
    }

    [Command("rank")]
    [Alias("level")]
    [Summary("Show your text XP rank card, or another member's.")]
    public async Task RankAsync([Remainder] string? query = null)
    {
        if (!await EnsureGuildAsync())
            return;

        var member = await ResolveMemberAsync(query);

        if (member is null)
            return;

        var record = _levelService.GetUser(Context.Guild.Id, member.Id);
        var rank = _levelService.GetTextRank(Context.Guild.Id, member.Id);

        await ReplyComponentsAsync(_builder.BuildRankCard(
            member,
            record.Xp,
            rank.Position,
            rank.Total,
            _levelService.IsPersistent));
    }

    [Command("vcrank")]
    [Alias("voicerank")]
    [Summary("Show your voice XP rank card, or another member's.")]
    public async Task VoiceRankAsync([Remainder] string? query = null)
    {
        if (!await EnsureGuildAsync())
            return;

        var member = await ResolveMemberAsync(query);

        if (member is null)
            return;

        var record = _levelService.GetUser(Context.Guild.Id, member.Id);
        var rank = _levelService.GetVoiceRank(Context.Guild.Id, member.Id);

        await ReplyComponentsAsync(_builder.BuildVoiceRankCard(
            member,
            record.VoiceXp,
            rank.Position,
            rank.Total,
            _levelService.IsPersistent));
    }

    [Command("leaderboard")]
    [Alias("lb", "top")]
    [Summary("Show the text XP leaderboard.")]
    public async Task LeaderboardAsync()
    {
        if (!await EnsureGuildAsync())
            return;

        var ranked = _levelService.GetTextLeaderboard(Context.Guild.Id);

        await ReplyComponentsAsync(_builder.BuildLeaderboard(
            LevelBoard.Text,
            ranked,
            Context.Guild,
            page: 0,
            requesterId: Context.User.Id));
    }

    [Command("vclb")]
    [Alias("voiceleaderboard", "vctop")]
    [Summary("Show the voice XP leaderboard.")]
    public async Task VoiceLeaderboardAsync()
    {
        if (!await EnsureGuildAsync())
            return;

        var ranked = _levelService.GetVoiceLeaderboard(Context.Guild.Id);

        await ReplyComponentsAsync(_builder.BuildLeaderboard(
            LevelBoard.Voice,
            ranked,
            Context.Guild,
            page: 0,
            requesterId: Context.User.Id));
    }

    [Command("levelenable")]
    [Alias("levelon")]
    [Summary("Turn the leveling system on.")]
    public async Task LevelEnableAsync()
    {
        if (!await EnsureAllowedAsync())
            return;

        var persisted = await _configService.SetEnabledAsync(Context.Guild.Id, true);

        await ReplyResultAsync(
            "Leveling Enabled",
            $"Members now earn XP from chatting and voice activity. Use `{Prefix}levelconfig` to review the settings.",
            persisted);
    }

    [Command("leveldisable")]
    [Alias("leveloff")]
    [Summary("Turn the leveling system off.")]
    public async Task LevelDisableAsync()
    {
        if (!await EnsureAllowedAsync())
            return;

        var persisted = await _configService.SetEnabledAsync(Context.Guild.Id, false);

        await ReplyResultAsync(
            "Leveling Disabled",
            "No more XP will be awarded. Existing XP and settings are kept.",
            persisted);
    }

    [Command("levelconfig")]
    [Alias("levelsettings", "levelinfo")]
    [Summary("Show the current leveling configuration.")]
    public async Task LevelConfigAsync()
    {
        if (!await EnsureAllowedAsync())
            return;

        await ReplyComponentsAsync(_builder.BuildConfig(
            _configService.GetConfig(Context.Guild.Id),
            Context.Guild,
            Prefix,
            _configService.IsPersistent));
    }

    [Command("setcooldown")]
    [Summary("Set the XP cooldown between messages, in seconds.")]
    public async Task SetCooldownAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);

        if (parts.Length == 0 || !int.TryParse(parts[0], out var seconds))
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}setcooldown <seconds>` — for example `{Prefix}setcooldown 60`.");
            return;
        }

        if (seconds < LevelDefaults.MinCooldownSeconds ||
            seconds > LevelDefaults.MaxCooldownSeconds)
        {
            await ReplyNoticeAsync(
                "Invalid Cooldown",
                $"Pick a value between `{LevelDefaults.MinCooldownSeconds}` and " +
                $"`{LevelDefaults.MaxCooldownSeconds}` seconds.");
            return;
        }

        var persisted = await _configService.SetCooldownAsync(Context.Guild.Id, seconds);

        await ReplyResultAsync(
            "XP Cooldown Updated",
            seconds == 0
                ? "Every message now earns XP with no cooldown."
                : $"Members can earn XP once every `{seconds}` second(s).",
            persisted);
    }

    [Command("setxprate")]
    [Summary("Set the per-message XP minimum and maximum.")]
    public async Task SetXpRateAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);

        if (parts.Length < 2 ||
            !int.TryParse(parts[0], out var minimum) ||
            !int.TryParse(parts[1], out var maximum))
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}setxprate <min> <max>` — for example `{Prefix}setxprate 15 25`.");
            return;
        }

        if (minimum > maximum)
            (minimum, maximum) = (maximum, minimum);

        if (minimum < LevelDefaults.MinXpPerMessage ||
            maximum > LevelDefaults.MaxXpPerMessage)
        {
            await ReplyNoticeAsync(
                "Invalid XP Rate",
                $"Both values must sit between `{LevelDefaults.MinXpPerMessage}` and " +
                $"`{LevelDefaults.MaxXpPerMessage}`.");
            return;
        }

        var persisted = await _configService.SetXpRateAsync(
            Context.Guild.Id,
            minimum,
            maximum);

        await ReplyResultAsync(
            "XP Rate Updated",
            $"Each message now grants a random `{minimum}` - `{maximum}` XP.",
            persisted);
    }

    [Command("setrankchannel")]
    [Alias("setlevelchannel")]
    [Summary("Set the channel where level-up announcements are posted.")]
    public async Task SetRankChannelAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);

        if (parts.Length == 0)
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}setrankchannel #channel` to route announcements, or " +
                $"`{Prefix}setrankchannel disable` to post them where the level-up happens.");
            return;
        }

        if (parts[0].ToLowerInvariant() is "disable" or "off" or "none" or "clear" or "reset")
        {
            var cleared = await _configService.SetLevelUpChannelAsync(Context.Guild.Id, null);

            await ReplyResultAsync(
                "Announcement Channel Cleared",
                "Level-ups will now be announced in the channel where they happen.",
                cleared);
            return;
        }

        if (!TryResolveTextChannel(parts[0], out var channel))
        {
            await ReplyNoticeAsync(
                "Channel Not Found",
                "Mention a text channel in this server or pass its ID.");
            return;
        }

        var persisted = await _configService.SetLevelUpChannelAsync(
            Context.Guild.Id,
            channel.Id);

        await ReplyResultAsync(
            "Announcement Channel Set",
            $"Level-up announcements will be posted in {channel.Mention}.",
            persisted);
    }

    [Command("setlevelupmessage")]
    [Alias("setlevelmessage")]
    [Summary("Set the level-up announcement message.")]
    public async Task SetLevelUpMessageAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var trimmed = input?.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}setlevelupmessage <text>` — placeholders `{{user}}`, `{{username}}`, " +
                $"`{{level}}`, `{{server}}`. Use `{Prefix}setlevelupmessage disable` for the default.");
            return;
        }

        if (trimmed.ToLowerInvariant() is "disable" or "off" or "none" or "clear" or "reset" or "default")
        {
            var cleared = await _configService.SetLevelUpMessageAsync(Context.Guild.Id, null);

            await ReplyResultAsync(
                "Level Up Message Reset",
                "The default announcement message is back in use.",
                cleared);
            return;
        }

        if (trimmed.Length > LevelDefaults.MaxLevelUpMessageLength)
        {
            await ReplyNoticeAsync(
                "Message Too Long",
                $"Keep it under `{LevelDefaults.MaxLevelUpMessageLength}` characters.");
            return;
        }

        var persisted = await _configService.SetLevelUpMessageAsync(Context.Guild.Id, trimmed);

        await ReplyResultAsync(
            "Level Up Message Set",
            $"New announcement: {Inline(trimmed)}",
            persisted);
    }

    [Command("togglelevelup")]
    [Alias("togglelevelups")]
    [Summary("Turn level-up announcements on or off.")]
    public async Task ToggleLevelUpAsync()
    {
        if (!await EnsureAllowedAsync())
            return;

        var config = _configService.GetConfig(Context.Guild.Id);
        var enabled = !config.LevelUpAnnouncementsEnabled;

        var persisted = await _configService.SetLevelUpAnnouncementsAsync(
            Context.Guild.Id,
            enabled);

        await ReplyResultAsync(
            enabled ? "Level Up Announcements Enabled" : "Level Up Announcements Disabled",
            enabled
                ? "Members will be congratulated whenever they reach a new level."
                : "Level-ups will happen quietly — no announcement will be posted.",
            persisted);
    }

    [Command("levelrole")]
    [Alias("levelroles")]
    [Summary("Manage level role rewards and the stack/replace mode.")]
    public async Task LevelRoleAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);

        if (parts.Length == 0)
        {
            await ReplyLevelRoleUsageAsync();
            return;
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "add":
                await AddLevelRoleAsync(parts);
                return;
            case "remove":
            case "delete":
                await RemoveLevelRoleAsync(parts);
                return;
            case "list":
            case "show":
                await ReplyComponentsAsync(_builder.BuildLevelRoleList(
                    _configService.GetConfig(Context.Guild.Id),
                    Prefix,
                    _configService.IsPersistent));
                return;
            case "mode":
                await SetLevelRoleModeAsync(parts);
                return;
            default:
                await ReplyLevelRoleUsageAsync();
                return;
        }
    }

    [Command("blacklistchannel")]
    [Alias("levelblacklistchannel", "xpblacklistchannel")]
    [Summary("Block channels from granting XP.")]
    public async Task BlacklistChannelAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);

        if (parts.Length == 0)
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}blacklistchannel <add|remove|list> [#channel]`");
            return;
        }

        var action = parts[0].ToLowerInvariant();

        if (action is "list" or "show")
        {
            await ReplyComponentsAsync(_builder.BuildChannelBlacklist(
                _configService.GetConfig(Context.Guild.Id),
                Prefix,
                _configService.IsPersistent));
            return;
        }

        if (action is not ("add" or "remove" or "delete"))
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}blacklistchannel <add|remove|list> [#channel]`");
            return;
        }

        if (parts.Length < 2 || !TryResolveTextChannel(parts[1], out var channel))
        {
            await ReplyNoticeAsync(
                "Channel Not Found",
                "Mention a text channel in this server or pass its ID.");
            return;
        }

        var update = action == "add"
            ? await _configService.AddBlacklistChannelAsync(Context.Guild.Id, channel.Id)
            : await _configService.RemoveBlacklistChannelAsync(Context.Guild.Id, channel.Id);

        await ReplyListUpdateAsync(
            update,
            "Channel Blacklist",
            added: $"{channel.Mention} will no longer grant XP.",
            alreadyPresent: $"{channel.Mention} is already blacklisted.",
            removed: $"{channel.Mention} can grant XP again.",
            notPresent: $"{channel.Mention} is not blacklisted.",
            limitReached: $"The blacklist is full — `{LevelDefaults.MaxBlacklistedChannels}` channels max.");
    }

    [Command("blacklistrole")]
    [Alias("levelblacklistrole", "xpblacklistrole")]
    [Summary("Block roles from earning XP.")]
    public async Task BlacklistRoleAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);

        if (parts.Length == 0)
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}blacklistrole <add|remove|list> [@role]`");
            return;
        }

        var action = parts[0].ToLowerInvariant();

        if (action is "list" or "show")
        {
            await ReplyComponentsAsync(_builder.BuildRoleBlacklist(
                _configService.GetConfig(Context.Guild.Id),
                Prefix,
                _configService.IsPersistent));
            return;
        }

        if (action is not ("add" or "remove" or "delete"))
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}blacklistrole <add|remove|list> [@role]`");
            return;
        }

        if (parts.Length < 2 || !TryResolveRole(parts[1], out var role))
        {
            await ReplyNoticeAsync(
                "Role Not Found",
                "Mention a role in this server or pass its ID.");
            return;
        }

        var update = action == "add"
            ? await _configService.AddBlacklistRoleAsync(Context.Guild.Id, role.Id)
            : await _configService.RemoveBlacklistRoleAsync(Context.Guild.Id, role.Id);

        await ReplyListUpdateAsync(
            update,
            "Role Blacklist",
            added: $"Members with {role.Mention} will no longer earn XP.",
            alreadyPresent: $"{role.Mention} is already blacklisted.",
            removed: $"Members with {role.Mention} can earn XP again.",
            notPresent: $"{role.Mention} is not blacklisted.",
            limitReached: $"The blacklist is full — `{LevelDefaults.MaxBlacklistedRoles}` roles max.");
    }

    [Command("addxp")]
    [Alias("givexp")]
    [Summary("Grant text XP to a member.")]
    public async Task AddXpAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        if (!TryReadAmount(input, out var member, out var amount, out var failure))
        {
            await ReplyNoticeAsync(failure.Title, failure.Message);
            return;
        }

        var update = await _levelService.AddTextXpAsync(member, amount);

        await ReplyResultAsync(
            "XP Granted",
            $"Gave `{amount:N0}` XP to {member.Mention}. " +
            $"They are now level `{update.CurrentLevel}` with `{update.TotalXp:N0}` XP.",
            update.Persisted);
    }

    [Command("removexp")]
    [Alias("takexp")]
    [Summary("Remove text XP from a member.")]
    public async Task RemoveXpAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        if (!TryReadAmount(input, out var member, out var amount, out var failure))
        {
            await ReplyNoticeAsync(failure.Title, failure.Message);
            return;
        }

        var update = await _levelService.RemoveTextXpAsync(member, amount);

        await ReplyResultAsync(
            "XP Removed",
            $"Took `{amount:N0}` XP from {member.Mention}. " +
            $"They are now level `{update.CurrentLevel}` with `{update.TotalXp:N0}` XP.",
            update.Persisted);
    }

    [Command("setlevel")]
    [Summary("Set a member's text level directly.")]
    public async Task SetLevelAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);

        if (parts.Length < 2 || !int.TryParse(parts[^1], out var level))
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}setlevel <member> <level>` — for example `{Prefix}setlevel @user 5`.");
            return;
        }

        if (level < 0 || level > LevelDefaults.MaxLevel)
        {
            await ReplyNoticeAsync(
                "Invalid Level",
                $"Pick a level between `0` and `{LevelDefaults.MaxLevel}`.");
            return;
        }

        var member = ResolveTarget(string.Join(' ', parts[..^1]));

        if (member is null)
        {
            await ReplyNoticeAsync("Member Not Found", "I could not find that member here.");
            return;
        }

        var update = await _levelService.SetTextLevelAsync(member, level);

        await ReplyResultAsync(
            "Level Set",
            $"{member.Mention} is now level `{update.CurrentLevel}` with `{update.TotalXp:N0}` XP.",
            update.Persisted);
    }

    [Command("rankreset")]
    [Alias("resetrank", "xpreset")]
    [Summary("Reset a member's text XP, or everyone's.")]
    public async Task RankResetAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);

        if (parts.Length == 0)
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}rankreset <member>` or `{Prefix}rankreset all`");
            return;
        }

        if (parts[0].ToLowerInvariant() is "all" or "everyone" or "server")
        {
            var wiped = await _levelService.ResetTextAllAsync(Context.Guild.Id);

            await ReplyResultAsync(
                "Text XP Reset",
                "Every member's text XP has been cleared. Voice XP is untouched.",
                wiped);
            return;
        }

        var member = ResolveTarget(string.Join(' ', parts));

        if (member is null)
        {
            await ReplyNoticeAsync("Member Not Found", "I could not find that member here.");
            return;
        }

        var persisted = await _levelService.ResetTextUserAsync(Context.Guild.Id, member.Id);
        await _levelService.SyncLevelRolesAsync(member);

        await ReplyResultAsync(
            "Text XP Reset",
            $"{member.Mention} is back to level `0`. Their voice XP is untouched.",
            persisted);
    }

    [Command("vcreset")]
    [Alias("voicereset", "resetvcrank")]
    [Summary("Reset a member's voice XP.")]
    public async Task VoiceResetAsync([Remainder] string? query = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        if (string.IsNullOrWhiteSpace(query))
        {
            await ReplyNoticeAsync("Usage", $"`{Prefix}vcreset <member>`");
            return;
        }

        var member = ResolveTarget(query.Trim());

        if (member is null)
        {
            await ReplyNoticeAsync("Member Not Found", "I could not find that member here.");
            return;
        }

        var persisted = await _levelService.ResetVoiceUserAsync(Context.Guild.Id, member.Id);

        await ReplyResultAsync(
            "Voice XP Reset",
            $"{member.Mention} is back to voice level `0`. Their text XP is untouched.",
            persisted);
    }

    [Command("vcresetall")]
    [Alias("voiceresetall")]
    [Summary("Reset every member's voice XP.")]
    public async Task VoiceResetAllAsync()
    {
        if (!await EnsureAllowedAsync())
            return;

        var wiped = await _levelService.ResetVoiceAllAsync(Context.Guild.Id);

        await ReplyResultAsync(
            "Voice XP Reset",
            "Every member's voice XP has been cleared. Text XP is untouched.",
            wiped);
    }

    private async Task AddLevelRoleAsync(string[] parts)
    {
        if (parts.Length < 3 ||
            !TryReadLevelRoleArguments(parts, out var role, out var level))
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}levelrole add @role <level>` — for example `{Prefix}levelrole add @Veteran 10`.");
            return;
        }

        if (role.Id == Context.Guild.EveryoneRole.Id || role.IsManaged)
        {
            await ReplyNoticeAsync(
                "Unusable Role",
                "Managed roles and `@everyone` cannot be handed out as rewards.");
            return;
        }

        if (role.Position >= Context.Guild.CurrentUser.Hierarchy)
        {
            await ReplyNoticeAsync(
                "Role Too High",
                $"{role.Mention} sits above my highest role, so I cannot assign it.");
            return;
        }

        var update = await _configService.AddLevelRoleAsync(Context.Guild.Id, level, role.Id);

        await ReplyListUpdateAsync(
            update,
            "Level Role",
            added: $"{role.Mention} will be granted at level `{level}`.",
            alreadyPresent: $"{role.Mention} is already the reward for level `{level}`.",
            removed: $"{role.Mention} is no longer a reward.",
            notPresent: $"{role.Mention} is not a reward yet.",
            limitReached: $"You already have `{LevelDefaults.MaxLevelRoles}` level roles configured.",
            invalid: $"Levels must be between `1` and `{LevelDefaults.MaxLevel}`.");
    }

    private async Task RemoveLevelRoleAsync(string[] parts)
    {
        if (parts.Length < 2)
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}levelrole remove @role` or `{Prefix}levelrole remove <level>`");
            return;
        }

        var token = parts[1];

        if (TryResolveRole(token, out var role))
        {
            var byRole = await _configService.RemoveLevelRoleByRoleAsync(
                Context.Guild.Id,
                role.Id);

            await ReplyListUpdateAsync(
                byRole,
                "Level Role",
                added: $"{role.Mention} added.",
                alreadyPresent: $"{role.Mention} is already a reward.",
                removed: $"{role.Mention} is no longer a level reward.",
                notPresent: $"{role.Mention} is not a level reward.",
                limitReached: "The reward list is full.");
            return;
        }

        if (!int.TryParse(token, out var level))
        {
            await ReplyNoticeAsync(
                "Role Not Found",
                "Mention a role, pass its ID, or give the level number to clear.");
            return;
        }

        var byLevel = await _configService.RemoveLevelRoleByLevelAsync(
            Context.Guild.Id,
            level);

        await ReplyListUpdateAsync(
            byLevel,
            "Level Role",
            added: $"Reward set for level `{level}`.",
            alreadyPresent: $"Level `{level}` already has that reward.",
            removed: $"Level `{level}` no longer grants a role.",
            notPresent: $"Level `{level}` has no reward configured.",
            limitReached: "The reward list is full.");
    }

    private async Task SetLevelRoleModeAsync(string[] parts)
    {
        if (parts.Length < 2 ||
            !LevelRoleModeExtensions.TryParse(parts[1], out var mode))
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}levelrole mode <stack|replace>` — `stack` keeps every earned " +
                "reward, `replace` keeps only the highest.");
            return;
        }

        var persisted = await _configService.SetRoleModeAsync(Context.Guild.Id, mode);

        await ReplyResultAsync(
            "Level Role Mode Updated",
            mode == LevelRoleMode.Stack
                ? "Members keep every reward role they earn."
                : "Members keep only their highest reward role — lower ones are removed.",
            persisted);
    }

    private Task ReplyLevelRoleUsageAsync()
    {
        return ReplyNoticeAsync(
            "Usage",
            $"`{Prefix}levelrole add @role <level>`\n" +
            $"`{Prefix}levelrole remove <@role|level>`\n" +
            $"`{Prefix}levelrole list`\n" +
            $"`{Prefix}levelrole mode <stack|replace>`");
    }

    private bool TryReadLevelRoleArguments(
        string[] parts,
        out SocketRole role,
        out int level)
    {
        role = null!;
        level = 0;

        // Both `add @role 10` and `add 10 @role` are accepted.
        if (TryResolveRole(parts[1], out var first) && int.TryParse(parts[2], out level))
        {
            role = first;
            return true;
        }

        if (int.TryParse(parts[1], out level) && TryResolveRole(parts[2], out var second))
        {
            role = second;
            return true;
        }

        return false;
    }

    private bool TryReadAmount(
        string? input,
        out SocketGuildUser member,
        out long amount,
        out (string Title, string Message) failure)
    {
        member = null!;
        amount = 0;
        failure = default;

        var parts = Split(input);

        if (parts.Length < 2 || !long.TryParse(parts[^1], out amount))
        {
            failure = ("Usage",
                $"`{Prefix}addxp <member> <amount>` / `{Prefix}removexp <member> <amount>`");
            return false;
        }

        if (amount <= 0 || amount > LevelDefaults.MaxXpGrant)
        {
            failure = ("Invalid Amount",
                $"Pick an amount between `1` and `{LevelDefaults.MaxXpGrant:N0}`.");
            return false;
        }

        var resolved = ResolveTarget(string.Join(' ', parts[..^1]));

        if (resolved is null)
        {
            failure = ("Member Not Found", "I could not find that member here.");
            return false;
        }

        member = resolved;
        return true;
    }

    private async Task<SocketGuildUser?> ResolveMemberAsync(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Context.User as SocketGuildUser;

        var member = ResolveTarget(query.Trim());

        if (member is null)
            await ReplyNoticeAsync("Member Not Found", "I could not find that member here.");

        return member;
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

    private bool TryResolveRole(string token, out SocketRole role)
    {
        role = null!;

        if (!MentionUtils.TryParseRole(token, out var roleId) &&
            !ulong.TryParse(token, out roleId))
        {
            return false;
        }

        var resolved = Context.Guild.GetRole(roleId);

        if (resolved is null)
            return false;

        role = resolved;
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
                "You need `Manage Server` or `Administrator` permission to manage leveling.");
            return false;
        }

        return true;
    }

    private Task ReplyListUpdateAsync(
        LevelListUpdate update,
        string title,
        string added,
        string alreadyPresent,
        string removed,
        string notPresent,
        string limitReached,
        string? invalid = null)
    {
        return update.Result switch
        {
            LevelListResult.Added => ReplyResultAsync(
                $"{title} Updated", added, update.Persisted),
            LevelListResult.Removed => ReplyResultAsync(
                $"{title} Updated", removed, update.Persisted),
            LevelListResult.AlreadyPresent => ReplyNoticeAsync(
                "No Change", alreadyPresent),
            LevelListResult.NotPresent => ReplyNoticeAsync(
                "No Change", notPresent),
            LevelListResult.LimitReached => ReplyNoticeAsync(
                "Limit Reached", limitReached),
            _ => ReplyNoticeAsync(
                "Invalid Input", invalid ?? "That value is not valid.")
        };
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

    private Task ReplyComponentsAsync(MessageComponent components)
    {
        return ReplyAsync(allowedMentions: AllowedMentions.None, components: components);
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
