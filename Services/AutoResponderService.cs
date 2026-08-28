using Discord;
using Discord.WebSocket;
using LastRide.Core;

namespace LastRide.Services;

/// <summary>
/// Watches guild messages and posts the configured reply when a message
/// contains one of the guild's trigger phrases. Wired into the message pipeline
/// by <see cref="Core.CommandHandler"/>.
/// </summary>
public sealed class AutoResponderService
{
    // Autoresponder replies may ping the users and roles named in the reply —
    // that is the point of the feature — but never @everyone/@here, so a
    // frequently-hit trigger can't be weaponised into a mass-ping.
    private static readonly AllowedMentions ReplyMentions = new()
    {
        AllowedTypes = AllowedMentionTypes.Users | AllowedMentionTypes.Roles
    };

    private readonly AutoResponderConfigService _configService;

    public AutoResponderService(AutoResponderConfigService configService)
    {
        _configService = configService;
    }

    public async Task HandleMessageAsync(SocketUserMessage message)
    {
        try
        {
            if (message.Channel is not SocketGuildChannel guildChannel)
                return;

            // Bots and webhooks never trigger a reply (avoids loops between bots).
            if (message.Author.IsBot || message.Author.IsWebhook)
                return;

            var config = _configService.GetConfig(guildChannel.Guild.Id);

            if (config.Responses.Count == 0)
                return;

            var content = message.Content;

            if (string.IsNullOrWhiteSpace(content))
                return;

            var reply = MatchReply(content, config.Responses);

            if (string.IsNullOrEmpty(reply))
                return;

            // Ping the users/roles named in the reply, but @everyone/@here are
            // suppressed by ReplyMentions to prevent mass-ping abuse.
            await message.Channel.SendMessageAsync(
                reply,
                allowedMentions: ReplyMentions);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[AutoResponder Error] {DiscordFailure.Format(exception)}");
        }
    }

    // Returns the reply for the first trigger contained in the message
    // (case-insensitive). Iterating in a stable order keeps behaviour
    // predictable when several triggers could match the same message.
    private static string? MatchReply(
        string content,
        IReadOnlyDictionary<string, string> responses)
    {
        foreach (var pair in responses)
        {
            if (content.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        }

        return null;
    }
}
