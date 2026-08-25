using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Models;
using LastRide.Services;

namespace LastRide.Modules;

[Name("AutoMod")]
public sealed class AutoModModule : ModuleBase<SocketCommandContext>
{
    private readonly AutoModConfigService _configService;
    private readonly AutoModComponentBuilder _builder;
    private readonly PrefixService _prefixService;

    public AutoModModule(
        AutoModConfigService configService,
        AutoModComponentBuilder builder,
        PrefixService prefixService)
    {
        _configService = configService;
        _builder = builder;
        _prefixService = prefixService;
    }

    [Command("automod")]
    [Summary("Shows the AutoMod overview or toggles the master switch.")]
    public async Task AutoModAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);
        var prefix = Prefix;

        if (parts.Length == 0)
        {
            await ReplyComponentsAsync(_builder.BuildOverview(
                _configService.GetConfig(Context.Guild.Id),
                prefix,
                _configService.IsPersistent,
                Context.Client.CurrentUser.GetDisplayAvatarUrl(size: 256),
                Context.Client.CurrentUser.Username));
            return;
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "on":
            case "enable":
                await _configService.SetMasterAsync(Context.Guild.Id, true);
                await ReplyComponentsAsync(_builder.BuildRulesConfigurator(
                    "AutoMod Enabled",
                    "Pick the rules to switch on from the menu below.",
                    _configService.GetConfig(Context.Guild.Id),
                    Context.User.Id,
                    Context.Guild.Id,
                    _configService.IsPersistent,
                    prefix));
                break;
            case "off":
            case "disable":
                await _configService.SetMasterAsync(Context.Guild.Id, false);
                await ReplyComponentsAsync(
                    _builder.BuildMasterToggled(false, _configService.IsPersistent, prefix));
                break;
            default:
                await ReplyNoticeAsync(
                    "Invalid Usage",
                    $"Usage: `{prefix}automod` (overview), `{prefix}automod on`, or `{prefix}automod off`.");
                break;
        }
    }

    [Command("anticaps")]
    [Summary("Blocks messages that are mostly uppercase.")]
    public Task AntiCapsAsync([Remainder] string? input = null)
        => HandleRuleAsync(AutoModRuleType.AntiCaps, input);

    [Command("antiduplicate")]
    [Summary("Blocks the same message repeated many times.")]
    public Task AntiDuplicateAsync([Remainder] string? input = null)
        => HandleRuleAsync(AutoModRuleType.AntiDuplicate, input);

    [Command("antiemoji")]
    [Summary("Blocks messages with too many emojis.")]
    public Task AntiEmojiAsync([Remainder] string? input = null)
        => HandleRuleAsync(AutoModRuleType.AntiEmoji, input);

    [Command("antiinvite")]
    [Summary("Removes Discord invite links.")]
    public Task AntiInviteAsync([Remainder] string? input = null)
        => HandleRuleAsync(AutoModRuleType.AntiInvite, input);

    [Command("antilink")]
    [Summary("Removes links from members without link permission.")]
    public Task AntiLinkAsync([Remainder] string? input = null)
        => HandleRuleAsync(AutoModRuleType.AntiLink, input);

    [Command("antimention")]
    [Summary("Blocks messages with too many mentions.")]
    public Task AntiMentionAsync([Remainder] string? input = null)
        => HandleRuleAsync(AutoModRuleType.AntiMention, input);

    [Command("antispam")]
    [Summary("Blocks rapid message spam.")]
    public Task AntiSpamAsync([Remainder] string? input = null)
        => HandleRuleAsync(AutoModRuleType.AntiSpam, input);

    [Command("automodbypass")]
    [Alias("ambypass")]
    [Summary("Manages roles exempt from all AutoMod rules.")]
    public async Task AutoModBypassAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);
        var prefix = Prefix;

        if (parts.Length == 0 ||
            parts[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            await ReplyBypassListAsync(note: null);
            return;
        }

        var action = parts[0].ToLowerInvariant();

        if (action is not ("add" or "remove" or "del" or "delete"))
        {
            await ReplyNoticeAsync(
                "Invalid Usage",
                $"Usage: `{prefix}automodbypass add @role`, `{prefix}automodbypass remove @role`, or `{prefix}automodbypass list`.");
            return;
        }

        if (parts.Length < 2 || !TryResolveRole(parts[1], out var role))
        {
            await ReplyNoticeAsync(
                "Role Not Found",
                "Mention a role or provide a valid role ID.");
            return;
        }

        if (action == "add")
        {
            var result = await _configService.AddBypassRoleAsync(Context.Guild.Id, role.Id);

            switch (result.Result)
            {
                case BypassRoleResult.Added:
                    await ReplyBypassListAsync($"Added <@&{role.Id}> to the bypass list.");
                    break;
                case BypassRoleResult.AlreadyPresent:
                    await ReplyNoticeAsync(
                        "Already Added",
                        $"<@&{role.Id}> is already a bypass role.");
                    break;
                case BypassRoleResult.LimitReached:
                    await ReplyNoticeAsync(
                        "Limit Reached",
                        $"You can have at most `{AutoModConfigService.MaxBypassRoles}` bypass roles.");
                    break;
            }

            return;
        }

        var removal = await _configService.RemoveBypassRoleAsync(Context.Guild.Id, role.Id);

        switch (removal.Result)
        {
            case BypassRoleResult.Removed:
                await ReplyBypassListAsync($"Removed <@&{role.Id}> from the bypass list.");
                break;
            case BypassRoleResult.NotPresent:
                await ReplyNoticeAsync(
                    "Not Found",
                    $"<@&{role.Id}> is not a bypass role.");
                break;
        }
    }

    [Command("automodlog")]
    [Summary("Sets the channel where AutoMod actions are logged.")]
    public async Task AutoModLogAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);
        var prefix = Prefix;

        if (parts.Length == 0)
        {
            await ReplyComponentsAsync(_builder.BuildLogChannel(
                _configService.GetConfig(Context.Guild.Id).LogChannelId,
                note: null,
                _configService.IsPersistent,
                prefix));
            return;
        }

        var token = parts[0].ToLowerInvariant();

        if (token is "disable" or "off" or "none" or "clear")
        {
            await _configService.SetLogChannelAsync(Context.Guild.Id, null);
            await ReplyComponentsAsync(_builder.BuildLogChannel(
                null,
                "Log channel cleared.",
                _configService.IsPersistent,
                prefix));
            return;
        }

        if (!TryResolveTextChannel(parts[0], out var channel))
        {
            await ReplyNoticeAsync(
                "Channel Not Found",
                $"Mention a text channel or provide a valid ID. Use `{prefix}automodlog disable` to clear.");
            return;
        }

        await _configService.SetLogChannelAsync(Context.Guild.Id, channel.Id);
        await ReplyComponentsAsync(_builder.BuildLogChannel(
            channel.Id,
            "Log channel updated.",
            _configService.IsPersistent,
            prefix));
    }

    [Command("badwords")]
    [Alias("badword")]
    [Summary("Manages the custom banned-words filter.")]
    public async Task BadWordsAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);
        var prefix = Prefix;

        if (parts.Length == 0 ||
            parts[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            await ReplyBadWordsAsync(note: null);
            return;
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "on":
            case "enable":
                await _configService.SetRuleEnabledAsync(
                    Context.Guild.Id, AutoModRuleType.Badwords, true);
                await ReplyBadWordsAsync("Bad-words filter enabled.");
                break;
            case "off":
            case "disable":
                await _configService.SetRuleEnabledAsync(
                    Context.Guild.Id, AutoModRuleType.Badwords, false);
                await ReplyBadWordsAsync("Bad-words filter disabled.");
                break;
            case "action":
                if (parts.Length < 2 ||
                    !AutoModActionExtensions.TryParse(parts[1], out var action))
                {
                    await ReplyNoticeAsync(
                        "Invalid Action",
                        $"Usage: `{prefix}badwords action <delete|warn|mute|kick|ban>`.");
                    return;
                }

                await _configService.SetRuleActionAsync(
                    Context.Guild.Id, AutoModRuleType.Badwords, action);
                await ReplyBadWordsAsync($"Action set to `{action.ToDisplay()}`.");
                break;
            case "add":
                await HandleBadWordAddAsync(parts, prefix);
                break;
            case "remove":
            case "delete":
            case "del":
                await HandleBadWordRemoveAsync(parts, prefix);
                break;
            default:
                await ReplyNoticeAsync(
                    "Invalid Usage",
                    $"Usage: `{prefix}badwords add <word>`, `{prefix}badwords remove <word>`, `{prefix}badwords list`, `{prefix}badwords on/off`, `{prefix}badwords action <delete|warn|mute|kick|ban>`.");
                break;
        }
    }

    private async Task HandleBadWordAddAsync(string[] parts, string prefix)
    {
        // Everything after the "add" keyword is the word/phrase to blacklist.
        var word = string.Join(' ', parts.Skip(1)).Trim();

        if (word.Length == 0)
        {
            await ReplyNoticeAsync(
                "Missing Word",
                $"Usage: `{prefix}badwords add <word or phrase>`.");
            return;
        }

        var result = await _configService.AddBadWordAsync(Context.Guild.Id, word);

        switch (result.Result)
        {
            case BadWordResult.Added:
                await ReplyBadWordsAsync($"Added {Inline(word)} to the bad-words list.");
                break;
            case BadWordResult.AlreadyPresent:
                await ReplyNoticeAsync(
                    "Already Added",
                    $"{Inline(word)} is already on the bad-words list.");
                break;
            case BadWordResult.LimitReached:
                await ReplyNoticeAsync(
                    "Limit Reached",
                    $"You can have at most `{AutoModConfigService.MaxBadWords}` bad words.");
                break;
            case BadWordResult.Invalid:
                await ReplyNoticeAsync(
                    "Invalid Word",
                    $"A bad word must be between 1 and `{AutoModDefaults.MaxBadWordLength}` characters.");
                break;
        }
    }

    private async Task HandleBadWordRemoveAsync(string[] parts, string prefix)
    {
        var word = string.Join(' ', parts.Skip(1)).Trim();

        if (word.Length == 0)
        {
            await ReplyNoticeAsync(
                "Missing Word",
                $"Usage: `{prefix}badwords remove <word or phrase>`.");
            return;
        }

        var result = await _configService.RemoveBadWordAsync(Context.Guild.Id, word);

        switch (result.Result)
        {
            case BadWordResult.Removed:
                await ReplyBadWordsAsync($"Removed {Inline(word)} from the bad-words list.");
                break;
            case BadWordResult.NotPresent:
                await ReplyNoticeAsync(
                    "Not Found",
                    $"{Inline(word)} is not on the bad-words list.");
                break;
            case BadWordResult.Invalid:
                await ReplyNoticeAsync(
                    "Invalid Word",
                    "Provide the word you want to remove.");
                break;
        }
    }

    private Task ReplyBadWordsAsync(string? note)
    {
        var config = _configService.GetConfig(Context.Guild.Id);

        return ReplyComponentsAsync(_builder.BuildBadWords(
            config.IsRuleEnabled(AutoModRuleType.Badwords),
            config.GetAction(AutoModRuleType.Badwords),
            config.BadWords.ToArray(),
            note,
            _configService.IsPersistent,
            Prefix));
    }

    private async Task HandleRuleAsync(AutoModRuleType rule, string? input)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);
        var prefix = Prefix;
        var command = rule.CommandName();

        if (parts.Length == 0)
        {
            await ReplyRuleStatusAsync(rule);
            return;
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "on":
            case "enable":
                await _configService.SetRuleEnabledAsync(Context.Guild.Id, rule, true);
                await ReplyRuleStatusAsync(rule);
                break;
            case "off":
            case "disable":
                await _configService.SetRuleEnabledAsync(Context.Guild.Id, rule, false);
                await ReplyRuleStatusAsync(rule);
                break;
            case "action":
                if (parts.Length < 2 ||
                    !AutoModActionExtensions.TryParse(parts[1], out var action))
                {
                    await ReplyNoticeAsync(
                        "Invalid Action",
                        $"Usage: `{prefix}{command} action <delete|warn|mute|kick|ban>`.");
                    return;
                }

                await _configService.SetRuleActionAsync(Context.Guild.Id, rule, action);
                await ReplyRuleStatusAsync(rule);
                break;
            default:
                await ReplyNoticeAsync(
                    "Invalid Usage",
                    $"Usage: `{prefix}{command} on/off` or `{prefix}{command} action <delete|warn|mute|kick|ban>`.");
                break;
        }
    }

    private Task ReplyRuleStatusAsync(AutoModRuleType rule)
    {
        var config = _configService.GetConfig(Context.Guild.Id);

        return ReplyComponentsAsync(_builder.BuildRuleStatus(
            rule,
            config.IsRuleEnabled(rule),
            config.GetAction(rule),
            _configService.IsPersistent,
            Prefix));
    }

    private Task ReplyBypassListAsync(string? note)
    {
        var config = _configService.GetConfig(Context.Guild.Id);

        return ReplyComponentsAsync(_builder.BuildBypassList(
            config.BypassRoleIds.ToArray(),
            note,
            _configService.IsPersistent,
            Prefix));
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

    private async Task<bool> EnsureAllowedAsync()
    {
        if (Context.Guild is null)
        {
            await ReplyNoticeAsync(
                "Server Only",
                "This command can only be used in a server.");
            return false;
        }

        if (Context.User is not SocketGuildUser user ||
            !(user.GuildPermissions.ManageGuild || user.GuildPermissions.Administrator))
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "You need `Manage Server` or `Administrator` permission to manage AutoMod.");
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

    private static string Inline(string value) => $"`{value.Replace("`", "'")}`";
}
