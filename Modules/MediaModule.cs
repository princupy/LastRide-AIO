using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Services;

namespace LastRide.Modules;

[Name("Media")]
public sealed class MediaModule : ModuleBase<SocketCommandContext>
{
    private readonly MediaConfigService _configService;
    private readonly MediaComponentBuilder _builder;
    private readonly PrefixService _prefixService;

    public MediaModule(
        MediaConfigService configService,
        MediaComponentBuilder builder,
        PrefixService prefixService)
    {
        _configService = configService;
        _builder = builder;
        _prefixService = prefixService;
    }

    [Command("media")]
    [Alias("mediaonly", "mediachannel")]
    [Summary("Manage media-only channels and mention forwarding.")]
    public async Task MediaAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);

        if (parts.Length == 0)
        {
            await ReplyStatusAsync();
            return;
        }

        var arguments = parts.Skip(1).ToArray();

        switch (parts[0].ToLowerInvariant())
        {
            case "show":
            case "list":
            case "status":
            case "config":
                await ReplyStatusAsync();
                return;

            case "setup":
            case "add":
                await AddChannelsAsync(arguments);
                return;

            case "remove":
            case "delete":
                await RemoveChannelsAsync(arguments);
                return;

            case "chat":
            case "forward":
                await SetChatChannelAsync(arguments);
                return;

            case "on":
            case "enable":
                await SetEnabledAsync(true);
                return;

            case "off":
            case "disable":
                await SetEnabledAsync(false);
                return;

            case "reset":
                await ResetAsync();
                return;

            default:
                await ReplyUsageAsync();
                return;
        }
    }

    private async Task AddChannelsAsync(string[] tokens)
    {
        if (tokens.Length == 0)
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}media setup #channel [#channel …]` — one or more text " +
                "channels to enforce as media-only.");

            return;
        }

        if (!TryResolveTextChannels(tokens, out var channels, out var unresolved))
        {
            await ReplyNoticeAsync(
                "Channel Not Found",
                $"I could not resolve {unresolved}. Mention text channels in this " +
                "server or pass their IDs.");

            return;
        }

        var config = _configService.GetConfig(Context.Guild.Id);

        // Forwarding into a media-only channel would delete the card the moment it
        // lands, so the two roles are kept apart.
        if (config.ChatChannelId is { } chatChannelId &&
            channels.Any(channel => channel.Id == chatChannelId))
        {
            await ReplyNoticeAsync(
                "Channel In Use",
                $"<#{chatChannelId}> is the forward channel — pick a different one, " +
                $"or move forwarding with `{Prefix}media chat set #channel`.");

            return;
        }

        var update = await _configService.AddChannelsAsync(
            Context.Guild.Id,
            channels.Select(channel => channel.Id).ToArray(),
            enable: true);

        await ReplyChannelUpdateAsync(update, channels, config, added: true);
    }

    private async Task RemoveChannelsAsync(string[] tokens)
    {
        if (tokens.Length == 0)
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}media remove #channel [#channel …]` — one or more " +
                "channels to stop enforcing.");

            return;
        }

        if (!TryResolveTextChannels(tokens, out var channels, out var unresolved))
        {
            await ReplyNoticeAsync(
                "Channel Not Found",
                $"I could not resolve {unresolved}. Mention text channels in this " +
                "server or pass their IDs.");

            return;
        }

        var config = _configService.GetConfig(Context.Guild.Id);

        var update = await _configService.RemoveChannelsAsync(
            Context.Guild.Id,
            channels.Select(channel => channel.Id).ToArray());

        await ReplyChannelUpdateAsync(update, channels, config, added: false);
    }

    private async Task SetChatChannelAsync(string[] tokens)
    {
        if (tokens.Length == 0)
        {
            await ReplyNoticeAsync(
                "Usage",
                $"`{Prefix}media chat set #channel` to pick where mentions are " +
                $"forwarded, or `{Prefix}media chat remove` to clear it.");

            return;
        }

        var action = tokens[0].ToLowerInvariant();

        if (action is "remove" or "clear" or "disable" or "off" or "none" or "reset")
        {
            var cleared = await _configService.SetChatChannelAsync(
                Context.Guild.Id,
                null);

            await ReplyResultAsync(
                "Forward Channel Cleared",
                "Removed messages will no longer be forwarded anywhere.",
                cleared);

            return;
        }

        var token = tokens[0];

        // `set` is optional, so `media chat set #general` and `media chat #general`
        // resolve the same channel.
        if (action is "set" or "channel")
        {
            if (tokens.Length < 2)
            {
                await ReplyNoticeAsync("Usage", $"`{Prefix}media chat set #channel`");
                return;
            }

            token = tokens[1];
        }

        if (!TryResolveTextChannel(token, out var channel))
        {
            await ReplyNoticeAsync(
                "Channel Not Found",
                "Mention a text channel in this server or pass its ID.");

            return;
        }

        var config = _configService.GetConfig(Context.Guild.Id);

        if (config.ChannelIds.Contains(channel.Id))
        {
            await ReplyNoticeAsync(
                "Channel In Use",
                $"{channel.Mention} is media-only, so forwarded cards would be " +
                $"removed there. Free it with `{Prefix}media remove " +
                $"{channel.Mention}` first.");

            return;
        }

        var persisted = await _configService.SetChatChannelAsync(
            Context.Guild.Id,
            channel.Id);

        await ReplyResultAsync(
            "Forward Channel Set",
            $"A removed message that mentions someone is now forwarded to " +
            $"{channel.Mention} with a ping.",
            persisted);
    }

    private async Task SetEnabledAsync(bool enabled)
    {
        var persisted = await _configService.SetEnabledAsync(
            Context.Guild.Id,
            enabled);

        var config = _configService.GetConfig(Context.Guild.Id);

        // Enabling with an empty list does nothing, so the card says so instead of
        // looking finished.
        if (enabled && config.ChannelIds.Count == 0)
        {
            await ReplyResultAsync(
                "Media Mode Enabled",
                "Nothing is enforced yet — add a channel with " +
                $"`{Prefix}media setup #channel`.",
                persisted);

            return;
        }

        await ReplyResultAsync(
            enabled ? "Media Mode Enabled" : "Media Mode Disabled",
            enabled
                ? $"`{config.ChannelIds.Count}` channel(s) are enforced as media-only."
                : "Text is allowed in every channel again.",
            persisted);
    }

    private async Task ResetAsync()
    {
        var persisted = await _configService.ResetAsync(Context.Guild.Id);

        await ReplyResultAsync(
            "Media Configuration Reset",
            "Every media-only channel and the forward channel were cleared.",
            persisted);
    }

    private async Task ReplyChannelUpdateAsync(
        MediaChannelUpdate update,
        IReadOnlyCollection<SocketTextChannel> channels,
        Models.MediaConfig config,
        bool added)
    {
        if (update.Changed == 0)
        {
            await ReplyNoticeAsync(
                added ? "Nothing Added" : "Nothing Removed",
                update.LimitReached
                    ? "The media-only list is full — " +
                      $"`{MediaConfigService.MaxChannels}` channels max."
                    : added
                        ? "Every channel you listed is already media-only."
                        : "None of the channels you listed were media-only.");

            return;
        }

        // A single channel is the common case and reads better named than counted.
        var subject = channels.Count == 1
            ? $"{channels.First().Mention} is"
            : $"`{update.Changed}` channel(s) are";

        var lines = new List<string>
        {
            added
                ? $"{subject} now media-only — only images, videos, files, " +
                  "stickers and links are allowed there."
                : $"{subject} no longer media-only."
        };

        if (update.Skipped > 0)
        {
            lines.Add(added
                ? $"`{update.Skipped}` were already media-only."
                : $"`{update.Skipped}` were not media-only.");
        }

        if (update.LimitReached)
        {
            lines.Add(
                "The list is now full — " +
                $"`{MediaConfigService.MaxChannels}` channels max.");
        }

        if (added && !config.ForwardsMentions)
        {
            lines.Add(
                $"Set `{Prefix}media chat set #channel` to forward removed " +
                "messages that mention someone.");
        }

        await ReplyResultAsync(
            added ? "Media Channels Added" : "Media Channels Removed",
            string.Join(" ", lines),
            update.Persisted);
    }

    private Task ReplyStatusAsync()
    {
        return ReplyComponentsAsync(_builder.BuildStatus(
            _configService.GetConfig(Context.Guild.Id),
            Context.Guild,
            Prefix,
            _configService.IsPersistent));
    }

    private Task ReplyUsageAsync()
    {
        return ReplyNoticeAsync(
            "Invalid Usage",
            $"Usage: `{Prefix}media setup #channel`, `{Prefix}media remove #channel`, " +
            $"`{Prefix}media show`, `{Prefix}media chat set #channel`, " +
            $"`{Prefix}media on/off`, `{Prefix}media reset`.");
    }

    private bool TryResolveTextChannels(
        IReadOnlyList<string> tokens,
        out List<SocketTextChannel> channels,
        out string unresolved)
    {
        channels = new List<SocketTextChannel>();
        var missing = new List<string>();

        foreach (var token in tokens)
        {
            if (!TryResolveTextChannel(token, out var channel))
            {
                missing.Add(Inline(token));
                continue;
            }

            // A channel listed twice should not be counted twice.
            if (channels.All(existing => existing.Id != channel.Id))
                channels.Add(channel);
        }

        unresolved = string.Join(", ", missing);
        return missing.Count == 0;
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
                "media-only channels.");

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
