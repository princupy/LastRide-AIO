using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Services;

namespace LastRide.Modules;

[Name("AutoResponder")]
public sealed class AutoResponderModule : ModuleBase<SocketCommandContext>
{
    private readonly AutoResponderConfigService _configService;
    private readonly AutoResponderComponentBuilder _builder;
    private readonly PrefixService _prefixService;

    public AutoResponderModule(
        AutoResponderConfigService configService,
        AutoResponderComponentBuilder builder,
        PrefixService prefixService)
    {
        _configService = configService;
        _builder = builder;
        _prefixService = prefixService;
    }

    [Command("autoresponder")]
    [Alias("autoresponse", "autoreply", "responder", "ar")]
    [Summary("Sets up automatic replies to trigger words and phrases.")]
    public async Task AutoResponderAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var prefix = Prefix;
        var trimmed = input?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            await ReplyListAsync();
            return;
        }

        var (action, rest) = SplitFirstWord(trimmed);

        switch (action.ToLowerInvariant())
        {
            case "list":
            case "status":
            case "show":
                await ReplyListAsync();
                break;
            case "add":
            case "create":
            case "set":
                await HandleAddAsync(rest);
                break;
            case "edit":
            case "update":
            case "change":
                await HandleEditAsync(rest);
                break;
            case "remove":
            case "delete":
            case "del":
            case "rem":
                await HandleRemoveAsync(rest);
                break;
            default:
                await ReplyNoticeAsync(
                    "Invalid Usage",
                    $"Usage: `{prefix}autoresponder add <trigger> <reply>`, `{prefix}autoresponder edit <trigger> <reply>`, `{prefix}autoresponder remove <trigger>`, `{prefix}autoresponder list`.");
                break;
        }
    }

    private async Task HandleAddAsync(string rest)
    {
        // First word is the trigger, everything after it is the reply — no
        // separator needed (e.g. `ar add hello Hey there!`).
        var (trigger, reply) = SplitFirstWord(rest);

        if (trigger.Length == 0 || reply.Length == 0)
        {
            await ReplyNoticeAsync(
                "Invalid Usage",
                $"Provide a trigger word followed by the reply. Example: `{Prefix}autoresponder add hello Hey there!`");
            return;
        }

        if (trigger.Length > AutoResponderConfigService.MaxTriggerLength)
        {
            await ReplyNoticeAsync(
                "Trigger Too Long",
                $"Triggers can be at most `{AutoResponderConfigService.MaxTriggerLength}` characters.");
            return;
        }

        if (reply.Length > AutoResponderConfigService.MaxReplyLength)
        {
            await ReplyNoticeAsync(
                "Reply Too Long",
                $"Replies can be at most `{AutoResponderConfigService.MaxReplyLength}` characters.");
            return;
        }

        var result = await _configService.AddResponseAsync(
            Context.Guild.Id,
            trigger,
            reply);

        switch (result.Result)
        {
            case ResponderResult.Added:
                await ReplySuccessAsync($"Added an autoresponder for `{Sanitize(trigger)}`.");
                break;
            case ResponderResult.Updated:
                await ReplySuccessAsync($"Updated the reply for `{Sanitize(trigger)}`.");
                break;
            case ResponderResult.LimitReached:
                await ReplyNoticeAsync(
                    "Limit Reached",
                    $"You can have at most `{AutoResponderConfigService.MaxResponders}` autoresponders.");
                break;
        }
    }

    private async Task HandleEditAsync(string rest)
    {
        // Same shape as add — first word is the trigger, the rest is the new
        // reply — but the trigger must already exist.
        var (trigger, reply) = SplitFirstWord(rest);

        if (trigger.Length == 0 || reply.Length == 0)
        {
            await ReplyNoticeAsync(
                "Invalid Usage",
                $"Provide the trigger to edit followed by the new reply. Example: `{Prefix}autoresponder edit hello Hi there!`");
            return;
        }

        if (reply.Length > AutoResponderConfigService.MaxReplyLength)
        {
            await ReplyNoticeAsync(
                "Reply Too Long",
                $"Replies can be at most `{AutoResponderConfigService.MaxReplyLength}` characters.");
            return;
        }

        var result = await _configService.EditResponseAsync(
            Context.Guild.Id,
            trigger,
            reply);

        switch (result.Result)
        {
            case ResponderResult.Updated:
                await ReplySuccessAsync($"Updated the reply for `{Sanitize(trigger)}`.");
                break;
            case ResponderResult.NotPresent:
                await ReplyNoticeAsync(
                    "Not Found",
                    $"There is no autoresponder for `{Sanitize(trigger)}`. Add it with `{Prefix}autoresponder add <trigger> <reply>`.");
                break;
        }
    }

    private async Task HandleRemoveAsync(string rest)
    {
        var trigger = rest.Trim();

        if (trigger.Length == 0)
        {
            await ReplyNoticeAsync(
                "Invalid Usage",
                $"Provide the trigger to remove: `{Prefix}autoresponder remove <trigger>`.");
            return;
        }

        var result = await _configService.RemoveResponseAsync(
            Context.Guild.Id,
            trigger);

        switch (result.Result)
        {
            case ResponderResult.Removed:
                await ReplySuccessAsync($"Removed the autoresponder for `{Sanitize(trigger)}`.");
                break;
            case ResponderResult.NotPresent:
                await ReplyNoticeAsync(
                    "Not Found",
                    $"There is no autoresponder for `{Sanitize(trigger)}`.");
                break;
        }
    }

    private Task ReplyListAsync()
    {
        var config = _configService.GetConfig(Context.Guild.Id);

        return ReplyComponentsAsync(_builder.BuildResponderList(
            config.Responses,
            AutoResponderConfigService.MaxResponders,
            _configService.IsPersistent,
            Prefix));
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
                "You need `Manage Server` or `Administrator` permission to manage autoresponders.");
            return false;
        }

        return true;
    }

    private string Prefix => _prefixService.GetPrefix(Context.Guild?.Id);

    private static (string Action, string Remainder) SplitFirstWord(string input)
    {
        var spaceIndex = input.IndexOf(' ');

        return spaceIndex < 0
            ? (input, string.Empty)
            : (input[..spaceIndex], input[(spaceIndex + 1)..].Trim());
    }

    private static string Sanitize(string value)
    {
        return value.Replace("`", "'");
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

    // Short one-line confirmation for add/edit/remove — the full list is only
    // shown by the list command.
    private Task ReplySuccessAsync(string message)
    {
        return ReplyComponentsAsync(_builder.BuildNotice("Autoresponder", message));
    }
}
