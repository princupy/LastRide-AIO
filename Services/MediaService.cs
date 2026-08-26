using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Models;

namespace LastRide.Services;

/// <summary>
/// Enforces the guild's media-only channels: anything without an attachment,
/// sticker or link is removed, and a removed message that mentioned somebody is
/// relayed to the configured chat channel so the conversation can continue there.
/// Wired into the message pipeline by <see cref="Core.CommandHandler"/>.
/// </summary>
public sealed partial class MediaService
{
    // Long enough to read, short enough that the channel stays clean.
    private const int NoticeSeconds = 6;

    // Upper bound on how many members one forwarded card may ping.
    private const int MaxForwardedMentions = 5;

    private readonly MediaConfigService _configService;
    private readonly MediaComponentBuilder _builder;

    public MediaService(
        MediaConfigService configService,
        MediaComponentBuilder builder)
    {
        _configService = configService;
        _builder = builder;
    }

    /// <summary>
    /// Returns <c>true</c> when the message was removed, signalling the caller to
    /// stop further processing of that message.
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

            if (!config.IsMediaChannel(guildChannel.Id))
                return false;

            if (HasMedia(message))
                return false;

            // Nobody is exempt — not admins, not the server owner. The only
            // consequence is a deletion, so there is none of the risk that makes
            // AutoMod exempt the owner from its punishments.
            //
            // Content and mentions are captured before the delete, because the
            // message object is the only place they exist afterwards.
            var content = message.Content ?? string.Empty;
            var mentionIds = ResolveMentions(message, author);

            await TryDeleteAsync(message);

            var forwarded = await TryForwardAsync(
                guild,
                config,
                author,
                guildChannel.Id,
                mentionIds,
                content);

            await SendNoticeAsync(guildChannel, author.Id, forwarded);

            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Media Scan Error] {exception}");
            return false;
        }
    }

    private static bool HasMedia(SocketUserMessage message)
    {
        // Embeds are deliberately not consulted: Discord has not unfurled the
        // links yet when a message first arrives, so a shared GIF or video has to
        // be recognised from the raw content instead.
        return message.Attachments.Count > 0 ||
            message.Stickers.Count > 0 ||
            LinkRegex().IsMatch(message.Content ?? string.Empty);
    }

    private static List<ulong> ResolveMentions(
        SocketUserMessage message,
        SocketGuildUser author)
    {
        // Self-mentions and bots are dropped: neither needs to be pinged into a
        // relayed conversation.
        return message
            .MentionedUsers
            .Where(user => user.Id != author.Id && !user.IsBot)
            .Select(user => user.Id)
            .Distinct()
            .Take(MaxForwardedMentions)
            .ToList();
    }

    private async Task<bool> TryForwardAsync(
        SocketGuild guild,
        MediaConfig config,
        SocketGuildUser author,
        ulong sourceChannelId,
        List<ulong> mentionIds,
        string content)
    {
        // Only a message aimed at somebody is worth relaying — plain chatter is
        // simply removed.
        if (mentionIds.Count == 0)
            return false;

        if (config.ChatChannelId is not { } chatChannelId)
            return false;

        if (guild.GetTextChannel(chatChannelId) is not { } chatChannel)
            return false;

        try
        {
            // Scoped ping so only the mentioned members are notified.
            // `AllowedTypes` has to be set explicitly: left unset the payload
            // carries no `parse` field and the whitelist is never applied, which
            // would let an @everyone inside the relayed text fire.
            await chatChannel.SendMessageAsync(
                allowedMentions: new AllowedMentions
                {
                    AllowedTypes = AllowedMentionTypes.None,
                    UserIds = mentionIds
                },
                components: _builder.BuildForward(
                    author,
                    sourceChannelId,
                    mentionIds,
                    content));

            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Media Forward Error] {exception.Message}");
            return false;
        }
    }

    private async Task SendNoticeAsync(
        SocketGuildChannel channel,
        ulong userId,
        bool forwarded)
    {
        if (channel is not IMessageChannel messageChannel)
            return;

        try
        {
            var sent = await messageChannel.SendMessageAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildViolationNotice(userId, forwarded));

            _ = DeleteAfterAsync(sent, TimeSpan.FromSeconds(NoticeSeconds));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Media Notice Error] {exception.Message}");
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
            Console.WriteLine($"[Media Delete Error] {exception.Message}");
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
        @"\b(?:https?://|www\.)?[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?\.(?:com|net|org|io|gg|me|xyz|co|tv|ru|in|info|link|app|dev|shop|store|online|site|club|live|fun|pro|biz|us|uk|ca|de|fr|nl|eu)(?:/[^\s]*)?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LinkRegex();
}
