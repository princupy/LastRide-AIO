using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Models;
using LastRide.Services;

namespace LastRide.Modules;

[Name("Logs")]
public sealed class LogModule : ModuleBase<SocketCommandContext>
{
    private readonly LogConfigService _configService;
    private readonly LogComponentBuilder _builder;
    private readonly PrefixService _prefixService;

    public LogModule(
        LogConfigService configService,
        LogComponentBuilder builder,
        PrefixService prefixService)
    {
        _configService = configService;
        _builder = builder;
        _prefixService = prefixService;
    }

    [Command("logconfig")]
    [Alias("logs", "loginfo")]
    [Summary("Shows the logging overview for this server.")]
    public async Task LogConfigAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        await ReplyComponentsAsync(_builder.BuildOverview(
            _configService.GetConfig(Context.Guild.Id),
            Prefix,
            _configService.IsPersistent,
            Context.Client.CurrentUser.GetDisplayAvatarUrl(size: 256),
            Context.Client.CurrentUser.Username));
    }

    [Command("logset")]
    [Summary("Sets or clears the channel for a log type.")]
    public async Task LogSetAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);
        var prefix = Prefix;

        if (parts.Length == 0 ||
            !LogTypeExtensions.TryParse(parts[0], out var type))
        {
            await ReplyNoticeAsync(
                "Invalid Usage",
                $"Usage: `{prefix}logset <messages|members|voice|moderation|roles|server> #channel` or `{prefix}logset <type> disable`.");
            return;
        }

        if (parts.Length < 2)
        {
            await ReplyComponentsAsync(_builder.BuildChannelResult(
                type,
                _configService.GetConfig(Context.Guild.Id).GetChannel(type),
                note: null,
                _configService.IsPersistent,
                prefix));
            return;
        }

        var token = parts[1].ToLowerInvariant();

        if (token is "disable" or "off" or "none" or "clear" or "reset")
        {
            await _configService.SetChannelAsync(Context.Guild.Id, type, null);
            await ReplyComponentsAsync(_builder.BuildChannelResult(
                type,
                null,
                $"{type.DisplayName()} log channel cleared.",
                _configService.IsPersistent,
                prefix));
            return;
        }

        if (!TryResolveTextChannel(parts[1], out var channel))
        {
            await ReplyNoticeAsync(
                "Channel Not Found",
                $"Mention a text channel or provide a valid ID. Use `{prefix}logset {type.ToString().ToLowerInvariant()} disable` to clear.");
            return;
        }

        await _configService.SetChannelAsync(Context.Guild.Id, type, channel.Id);
        await ReplyComponentsAsync(_builder.BuildChannelResult(
            type,
            channel.Id,
            $"{type.DisplayName()} log channel updated.",
            _configService.IsPersistent,
            prefix));
    }

    [Command("logenable")]
    [Alias("logon", "logsetup")]
    [Summary("Enables logging and opens the channel setup menu.")]
    public async Task LogEnableAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        await _configService.SetEnabledAsync(Context.Guild.Id, true);

        var bot = Context.Guild.CurrentUser;

        // The setup menu auto-creates a channel per selected type, which needs
        // Manage Channels. Without it we still flip the master switch on but
        // point the moderator at the manual route instead of a dead menu.
        if (!(bot.GuildPermissions.ManageChannels ||
              bot.GuildPermissions.Administrator))
        {
            await ReplyComponentsAsync(_builder.BuildMasterResult(
                true,
                _configService.IsPersistent,
                Prefix,
                $"I need the `Manage Channels` permission to auto-create log channels. Grant it and run `{Prefix}logenable` again, or route existing channels with `{Prefix}logset <type> #channel`."));
            return;
        }

        await ReplyComponentsAsync(_builder.BuildSetupConfigurator(
            "Logging Enabled",
            "Pick the logs you want — I'll create a channel for each and route it automatically.",
            _configService.GetConfig(Context.Guild.Id),
            Context.User.Id,
            Context.Guild.Id,
            _configService.IsPersistent,
            Prefix));
    }

    [Command("logdisable")]
    [Alias("logoff")]
    [Summary("Turns the logging master switch off.")]
    public async Task LogDisableAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        await _configService.SetEnabledAsync(Context.Guild.Id, false);
        await ReplyComponentsAsync(_builder.BuildMasterResult(
            false,
            _configService.IsPersistent,
            Prefix));
    }

    [Command("logreset")]
    [Summary("Clears all logging settings for this server.")]
    public async Task LogResetAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        await _configService.ResetAsync(Context.Guild.Id);
        await ReplyNoticeAsync(
            "Logging Reset",
            "All logging settings have been cleared for this server.");
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
                "You need `Manage Server` or `Administrator` permission to manage logging.");
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
}
