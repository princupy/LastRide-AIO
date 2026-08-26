using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Services;

namespace LastRide.Modules;

[Name("Welcome")]
public sealed class WelcomeModule : ModuleBase<SocketCommandContext>
{
    private readonly WelcomeConfigService _configService;
    private readonly WelcomeComponentBuilder _builder;
    private readonly WelcomeService _welcomeService;
    private readonly PrefixService _prefixService;

    public WelcomeModule(
        WelcomeConfigService configService,
        WelcomeComponentBuilder builder,
        WelcomeService welcomeService,
        PrefixService prefixService)
    {
        _configService = configService;
        _builder = builder;
        _welcomeService = welcomeService;
        _prefixService = prefixService;
    }

    [Command("welcome")]
    [Alias("greet")]
    [Summary("Turn the welcome greeting on or off, preview it, or reset it.")]
    public async Task WelcomeAsync([Remainder] string? input = null)
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

            case "test":
            case "preview":
                await SendTestAsync();
                return;

            case "reset":
                await _configService.ResetAsync(Context.Guild.Id);
                await ReplyStatusAsync("Welcome configuration reset.");
                return;

            default:
                await ReplyNoticeAsync(
                    "Invalid Usage",
                    $"Usage: `{Prefix}welcome on/off`, `{Prefix}welcome test`, " +
                    $"`{Prefix}welcome status`, `{Prefix}welcome reset`.");
                return;
        }
    }

    [Command("welcomechannel")]
    [Alias("greetchannel")]
    [Summary("Set the channel where welcome messages are posted.")]
    public async Task WelcomeChannelAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);

        if (parts.Length == 0)
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}welcomechannel set #channel` to pick the greeting channel, " +
                $"or `{Prefix}welcomechannel remove` to clear it.");

            return;
        }

        var action = parts[0].ToLowerInvariant();

        if (action is "remove" or "clear" or "disable" or "off" or "none" or "reset")
        {
            var cleared = await _configService.SetChannelAsync(Context.Guild.Id, null);

            await ReplyResultAsync(
                "Welcome Channel Cleared",
                "No greeting will be posted until a channel is set again.",
                cleared);

            return;
        }

        var token = parts[0];

        // `set` is optional, so `welcomechannel set #general` and
        // `welcomechannel #general` resolve the same channel.
        if (action is "set" or "channel")
        {
            if (parts.Length < 2)
            {
                await ReplyNoticeAsync(
                    "Usage",
                    $"`{Prefix}welcomechannel set #channel`");

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

        var persisted = await _configService.SetChannelAsync(
            Context.Guild.Id,
            channel.Id);

        // Setting a channel alone does nothing until greetings are switched on,
        // so the card says so instead of looking finished.
        var enabled = _configService.GetConfig(Context.Guild.Id).Enabled;

        await ReplyResultAsync(
            "Welcome Channel Set",
            $"New members will be greeted in {channel.Mention}." +
            (enabled ? string.Empty : $" Turn it on with `{Prefix}welcome on`."),
            persisted);
    }

    [Command("welcomemessage")]
    [Alias("greetmessage")]
    [Summary("Set the welcome message shown to new members.")]
    public async Task WelcomeMessageAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var trimmed = input?.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}welcomemessage <text>` — placeholders `{{user}}`, " +
                $"`{{username}}`, `{{server}}`, `{{membercount}}`. Use " +
                $"`{Prefix}welcomemessage reset` for the default.");

            return;
        }

        if (trimmed.ToLowerInvariant() is "disable" or "off" or "none" or "clear" or "reset" or "default")
        {
            var cleared = await _configService.SetMessageAsync(Context.Guild.Id, null);

            await ReplyResultAsync(
                "Welcome Message Reset",
                "The default greeting is back in use.",
                cleared);

            return;
        }

        if (trimmed.Length > WelcomeConfigService.MaxMessageLength)
        {
            await ReplyNoticeAsync(
                "Message Too Long",
                $"Keep it under `{WelcomeConfigService.MaxMessageLength}` characters.");

            return;
        }

        var persisted = await _configService.SetMessageAsync(Context.Guild.Id, trimmed);

        await ReplyResultAsync(
            "Welcome Message Set",
            $"New greeting: {Inline(trimmed)}",
            persisted);
    }

    private async Task SetEnabledAsync(bool enabled)
    {
        await _configService.SetEnabledAsync(Context.Guild.Id, enabled);

        await ReplyStatusAsync(enabled
            ? "Welcome greetings enabled."
            : "Welcome greetings disabled.");
    }

    private async Task SendTestAsync()
    {
        if (Context.User is not SocketGuildUser member)
            return;

        var outcome = await _welcomeService.SendTestAsync(member);

        await (outcome.Result switch
        {
            WelcomeSendResult.Sent => ReplyResultAsync(
                "Test Greeting Sent",
                $"A preview of the welcome card was posted in <#{outcome.ChannelId}>.",
                _configService.IsPersistent),

            WelcomeSendResult.ChannelNotSet => ReplyNoticeAsync(
                "Channel Not Set",
                $"Set a welcome channel first with `{Prefix}welcomechannel set #channel`."),

            WelcomeSendResult.ChannelMissing => ReplyNoticeAsync(
                "Channel Missing",
                "The configured welcome channel no longer exists. Set a new one " +
                $"with `{Prefix}welcomechannel set #channel`."),

            _ => ReplyNoticeAsync(
                "Send Failed",
                $"I could not post in <#{outcome.ChannelId}> — check that I have " +
                "`View Channel` and `Send Messages` there.")
        });
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
                "You need `Manage Server` or `Administrator` permission to manage " +
                "the welcome greeting.");

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
