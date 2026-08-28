using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Core;
using LastRide.Models;

namespace LastRide.Services;

/// <summary>
/// Scans incoming guild messages against each guild's enabled AutoMod rules and
/// applies the configured action. Wired into the message pipeline by
/// <see cref="Core.CommandHandler"/>.
/// </summary>
public sealed partial class AutoModService
{
    // Evaluation order. Frequency rules run first so their counters always
    // update before a content rule can short-circuit the scan. Anti-Invite runs
    // before Anti-Link because an invite is a more specific match.
    private static readonly AutoModRuleType[] EvaluationOrder =
    {
        AutoModRuleType.AntiSpam,
        AutoModRuleType.AntiDuplicate,
        AutoModRuleType.Badwords,
        AutoModRuleType.AntiInvite,
        AutoModRuleType.AntiLink,
        AutoModRuleType.AntiMention,
        AutoModRuleType.AntiCaps,
        AutoModRuleType.AntiEmoji
    };

    private readonly AutoModConfigService _configService;
    private readonly AutoModComponentBuilder _builder;

    private readonly ConcurrentDictionary<StateKey, Queue<DateTimeOffset>> _spam = new();
    private readonly ConcurrentDictionary<StateKey, DuplicateState> _duplicate = new();

    public AutoModService(
        AutoModConfigService configService,
        AutoModComponentBuilder builder)
    {
        _configService = configService;
        _builder = builder;
    }

    /// <summary>
    /// Returns <c>true</c> when the message was removed by AutoMod, signalling
    /// the caller to stop further processing of that message.
    /// </summary>
    public async Task<bool> ScanAsync(SocketUserMessage message)
    {
        try
        {
            if (message.Channel is not SocketGuildChannel guildChannel)
                return false;

            if (message.Author is not SocketGuildUser author)
                return false;

            var guild = guildChannel.Guild;
            var config = _configService.GetConfig(guild.Id);

            if (!config.Enabled)
                return false;

            // Exempt: only the server owner (always above the bot, cannot be
            // actioned) and members holding a configured bypass role. Admins are
            // NOT exempt — AutoMod applies to them until a bypass role is granted.
            if (author.Id == guild.OwnerId)
                return false;

            if (config.HasBypassRole(author.Roles.Select(role => role.Id)))
                return false;

            var content = message.Content ?? string.Empty;

            foreach (var rule in EvaluationOrder)
            {
                if (!config.IsRuleEnabled(rule))
                    continue;

                if (!Violates(rule, content, author, guildChannel, config))
                    continue;

                await ApplyActionAsync(
                    rule,
                    config.GetAction(rule),
                    message,
                    author,
                    guild,
                    guildChannel,
                    config.LogChannelId);

                return true;
            }

            return false;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[AutoMod Scan Error] {DiscordFailure.Format(exception)}");
            return false;
        }
    }

    private bool Violates(
        AutoModRuleType rule,
        string content,
        SocketGuildUser author,
        SocketGuildChannel channel,
        AutoModConfig config)
    {
        return rule switch
        {
            AutoModRuleType.AntiSpam => IsSpam(channel.Guild.Id, channel.Id, author.Id),
            AutoModRuleType.AntiDuplicate => IsDuplicate(channel.Guild.Id, channel.Id, author.Id, content),
            AutoModRuleType.Badwords => ContainsBadWord(content, config.BadWords),
            AutoModRuleType.AntiInvite => ContainsInvite(content),
            AutoModRuleType.AntiLink => !HasLinkBypass(author, channel) && ContainsLink(content),
            AutoModRuleType.AntiMention => ExceedsMentions(content),
            AutoModRuleType.AntiCaps => IsExcessiveCaps(content),
            AutoModRuleType.AntiEmoji => ExceedsEmojis(content),
            _ => false
        };
    }

    private static bool HasLinkBypass(
        SocketGuildUser author,
        SocketGuildChannel channel)
    {
        // Administrators are never auto-exempt from Anti-Link.
        if (author.GuildPermissions.Administrator)
            return false;

        // Only an INTENTIONAL, member- or role-specific "may post links here" grant
        // exempts someone from Anti-Link: an explicit ALLOW overwrite on this channel
        // for the member directly, or for one of their (non-@everyone) roles. The
        // @everyone baseline is ignored on purpose — Discord grants Embed Links to
        // @everyone by default, so honouring it would let every member bypass.
        // (Embed Links = links, Attach Files = uploaded gifs.)
        if (IsLinkAllowed(channel.GetPermissionOverwrite(author)))
            return true;

        var everyoneId = channel.Guild.EveryoneRole.Id;

        foreach (var role in author.Roles)
        {
            if (role.Id == everyoneId)
                continue;

            if (IsLinkAllowed(channel.GetPermissionOverwrite(role)))
                return true;
        }

        return false;
    }

    private static bool IsLinkAllowed(OverwritePermissions? overwrite)
    {
        return overwrite is { } permissions &&
            (permissions.EmbedLinks == PermValue.Allow ||
             permissions.AttachFiles == PermValue.Allow);
    }

    private bool IsSpam(ulong guildId, ulong channelId, ulong userId)
    {
        var key = new StateKey(guildId, channelId, userId);
        var now = DateTimeOffset.UtcNow;
        var window = TimeSpan.FromSeconds(AutoModDefaults.SpamWindowSeconds);
        var queue = _spam.GetOrAdd(key, _ => new Queue<DateTimeOffset>());

        lock (queue)
        {
            queue.Enqueue(now);

            while (queue.Count > 0 && now - queue.Peek() > window)
                queue.Dequeue();

            if (queue.Count < AutoModDefaults.SpamMessages)
                return false;

            queue.Clear();
            return true;
        }
    }

    private bool IsDuplicate(
        ulong guildId,
        ulong channelId,
        ulong userId,
        string content)
    {
        var normalized = content.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(normalized))
            return false;

        var key = new StateKey(guildId, channelId, userId);
        var now = DateTimeOffset.UtcNow;
        var window = TimeSpan.FromSeconds(AutoModDefaults.DuplicateWindowSeconds);
        var state = _duplicate.GetOrAdd(key, _ => new DuplicateState());

        lock (state)
        {
            if (state.Content == normalized && now - state.LastAt <= window)
            {
                state.Count++;
            }
            else
            {
                state.Content = normalized;
                state.Count = 1;
            }

            state.LastAt = now;

            if (state.Count < AutoModDefaults.DuplicateCount)
                return false;

            state.Count = 0;
            state.Content = string.Empty;
            return true;
        }
    }

    private static bool ContainsInvite(string content)
    {
        return InviteRegex().IsMatch(content);
    }

    private static bool ContainsLink(string content)
    {
        return LinkRegex().IsMatch(content);
    }

    private static bool ContainsBadWord(string content, IReadOnlySet<string> badWords)
    {
        if (badWords.Count == 0 || string.IsNullOrWhiteSpace(content))
            return false;

        // Whole-word match on each token so a banned word is not flagged when it
        // merely appears inside a larger, innocent word (the "Scunthorpe" problem).
        foreach (Match match in WordRegex().Matches(content))
        {
            if (badWords.Contains(match.Value))
                return true;
        }

        // Multi-word entries (containing a space) can't be matched token-by-token,
        // so fall back to a case-insensitive substring check for those.
        foreach (var word in badWords)
        {
            if (word.Contains(' ') &&
                content.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ExceedsMentions(string content)
    {
        // Count raw mention tokens (not distinct users) so pinging the same member
        // many times is caught exactly like mentioning many different members.
        var count = MentionRegex().Matches(content).Count;

        if (content.Contains("@everyone", StringComparison.OrdinalIgnoreCase))
            count++;

        if (content.Contains("@here", StringComparison.OrdinalIgnoreCase))
            count++;

        return count > AutoModDefaults.MaxMentions;
    }

    private static bool IsExcessiveCaps(string content)
    {
        var letters = 0;
        var upper = 0;

        foreach (var character in content)
        {
            if (!char.IsLetter(character))
                continue;

            letters++;

            if (char.IsUpper(character))
                upper++;
        }

        if (letters < AutoModDefaults.CapsMinLength)
            return false;

        return (double)upper / letters >= AutoModDefaults.CapsRatio;
    }

    private static bool ExceedsEmojis(string content)
    {
        var count = CustomEmojiRegex().Matches(content).Count;

        foreach (var rune in content.EnumerateRunes())
        {
            if (IsEmojiRune(rune.Value))
                count++;
        }

        return count > AutoModDefaults.MaxEmojis;
    }

    private static bool IsEmojiRune(int value)
    {
        return value is (>= 0x1F300 and <= 0x1FAFF)
            or (>= 0x1F000 and <= 0x1F0FF)
            or (>= 0x2600 and <= 0x27BF)
            or (>= 0x2B00 and <= 0x2BFF)
            or (>= 0x1F1E6 and <= 0x1F1FF);
    }

    private async Task ApplyActionAsync(
        AutoModRuleType rule,
        AutoModAction action,
        SocketUserMessage message,
        SocketGuildUser author,
        SocketGuild guild,
        SocketGuildChannel channel,
        ulong? logChannelId)
    {
        var reason = $"AutoMod: {rule.DisplayName()} violation.";

        await TryDeleteAsync(message);

        switch (action)
        {
            case AutoModAction.Mute:
                await TryTimeoutAsync(author, reason);
                break;
            case AutoModAction.Kick:
                await TryKickAsync(author, reason);
                break;
            case AutoModAction.Ban:
                await TryBanAsync(guild, author, reason);
                break;
        }

        await SendViolationNoticeAsync(channel, author.Id, rule, action);
        await SendLogAsync(guild, logChannelId, author, rule, action, channel.Id, message.Content);
    }

    private async Task SendViolationNoticeAsync(
        SocketGuildChannel channel,
        ulong userId,
        AutoModRuleType rule,
        AutoModAction action)
    {
        if (channel is not IMessageChannel messageChannel)
            return;

        try
        {
            var sent = await messageChannel.SendMessageAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildViolationNotice(userId, rule, action));

            _ = DeleteAfterAsync(sent, TimeSpan.FromSeconds(6));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[AutoMod Notice Error] {DiscordFailure.Summarize(exception)}");
        }
    }

    private async Task SendLogAsync(
        SocketGuild guild,
        ulong? logChannelId,
        SocketGuildUser author,
        AutoModRuleType rule,
        AutoModAction action,
        ulong channelId,
        string? content)
    {
        if (logChannelId is not { } id)
            return;

        if (guild.GetTextChannel(id) is not { } logChannel)
            return;

        try
        {
            await logChannel.SendMessageAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildLog(
                    author.Id,
                    author.Username,
                    author.GetDisplayAvatarUrl(size: 256),
                    rule,
                    action,
                    channelId,
                    content ?? string.Empty));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[AutoMod Log Error] {DiscordFailure.Summarize(exception)}");
        }
    }

    private static async Task TryDeleteAsync(SocketUserMessage message)
    {
        try
        {
            await message.DeleteAsync();
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[AutoMod Delete Error] {DiscordFailure.Summarize(exception)}");
        }
    }

    private static async Task TryTimeoutAsync(SocketGuildUser author, string reason)
    {
        try
        {
            await author.SetTimeOutAsync(
                TimeSpan.FromMinutes(AutoModDefaults.MuteMinutes),
                new RequestOptions { AuditLogReason = reason });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[AutoMod Mute Error] {DiscordFailure.Summarize(exception)}");
        }
    }

    private static async Task TryKickAsync(SocketGuildUser author, string reason)
    {
        try
        {
            await author.KickAsync(reason);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[AutoMod Kick Error] {DiscordFailure.Summarize(exception)}");
        }
    }

    private static async Task TryBanAsync(
        SocketGuild guild,
        SocketGuildUser author,
        string reason)
    {
        try
        {
            await guild.AddBanAsync(author.Id, 0, reason);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[AutoMod Ban Error] {DiscordFailure.Summarize(exception)}");
        }
    }

    private static async Task DeleteAfterAsync(IUserMessage message, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay);
            await message.DeleteAsync();
        }
        catch
        {
            // Notice already gone or missing permission — nothing to do.
        }
    }

    [GeneratedRegex(
        @"(?:discord(?:app)?\.com/invite|discord\.gg|discord\.io|discord\.me|dsc\.gg)/[A-Za-z0-9\-]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InviteRegex();

    [GeneratedRegex(
        @"\b(?:https?://|www\.)?[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?\.(?:com|net|org|io|gg|me|xyz|co|tv|ru|in|info|link|app|dev|shop|store|online|site|club|live|fun|pro|biz|us|uk|ca|de|fr|nl|eu)(?:/[^\s]*)?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LinkRegex();

    [GeneratedRegex(
        @"<a?:[A-Za-z0-9_]+:[0-9]+>",
        RegexOptions.CultureInvariant)]
    private static partial Regex CustomEmojiRegex();

    [GeneratedRegex(
        @"<@[!&]?[0-9]+>",
        RegexOptions.CultureInvariant)]
    private static partial Regex MentionRegex();

    [GeneratedRegex(
        @"[\p{L}\p{N}]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();

    private readonly record struct StateKey(ulong Guild, ulong Channel, ulong User);

    private sealed class DuplicateState
    {
        public string Content { get; set; } = string.Empty;
        public int Count { get; set; }
        public DateTimeOffset LastAt { get; set; }
    }
}
