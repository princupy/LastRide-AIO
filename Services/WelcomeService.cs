using Discord;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Core;

namespace LastRide.Services;

/// <summary>
/// Posts the guild's welcome greeting when a member joins, and renders the same
/// card on demand for <c>welcome test</c>. Wired to the gateway event by
/// <see cref="Core.CommandHandler"/>.
/// </summary>
public sealed class WelcomeService
{
    private const string DefaultMessage =
        "Welcome to **{server}**, {user}! Glad to have you here.";

    private readonly WelcomeConfigService _configService;
    private readonly WelcomeComponentBuilder _builder;

    public WelcomeService(
        WelcomeConfigService configService,
        WelcomeComponentBuilder builder)
    {
        _configService = configService;
        _builder = builder;
    }

    public async Task HandleUserJoinedAsync(SocketGuildUser user)
    {
        try
        {
            // Bots are added by staff rather than joining on their own, so only
            // real members are greeted.
            if (user.IsBot)
                return;

            var config = _configService.GetConfig(user.Guild.Id);

            if (!config.IsReady || config.ChannelId is not { } channelId)
                return;

            var channel = user.Guild.GetTextChannel(channelId);

            if (channel is null)
                return;

            await SendAsync(user, channel, config.Message, isTest: false);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Welcome Join Error] {DiscordFailure.Format(exception)}");
        }
    }

    /// <summary>
    /// Posts a preview of the greeting for the member who ran the command. The
    /// enabled flag is deliberately ignored so a server can check the card
    /// before switching greetings on.
    /// </summary>
    public async Task<WelcomeTestOutcome> SendTestAsync(SocketGuildUser member)
    {
        var config = _configService.GetConfig(member.Guild.Id);

        if (config.ChannelId is not { } channelId)
            return new WelcomeTestOutcome(WelcomeSendResult.ChannelNotSet, null);

        var channel = member.Guild.GetTextChannel(channelId);

        if (channel is null)
            return new WelcomeTestOutcome(WelcomeSendResult.ChannelMissing, channelId);

        var sent = await SendAsync(member, channel, config.Message, isTest: true);

        return new WelcomeTestOutcome(
            sent ? WelcomeSendResult.Sent : WelcomeSendResult.SendFailed,
            channelId);
    }

    private async Task<bool> SendAsync(
        SocketGuildUser member,
        SocketTextChannel channel,
        string? template,
        bool isTest)
    {
        var rendered = RenderMessage(template ?? DefaultMessage, member);
        var components = _builder.BuildWelcome(member, rendered, isTest);

        try
        {
            // Scoped ping so the new member actually gets a notification and can
            // find the greeting. `AllowedTypes` has to be set explicitly: with it
            // left unset the payload carries no `parse` field and the whitelist
            // below is never applied, so nobody was being pinged at all.
            await channel.SendMessageAsync(
                allowedMentions: new AllowedMentions
                {
                    AllowedTypes = AllowedMentionTypes.None,
                    UserIds = new List<ulong> { member.Id }
                },
                components: components);

            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Welcome Send Error] {DiscordFailure.Summarize(exception)}");
            return false;
        }
    }

    private static string RenderMessage(string template, SocketGuildUser member)
    {
        return template
            .Replace("{user}", member.Mention)
            .Replace("{username}", member.DisplayName)
            .Replace("{server}", member.Guild.Name)
            .Replace("{membercount}", member.Guild.MemberCount.ToString("N0"));
    }
}

public enum WelcomeSendResult
{
    Sent,
    ChannelNotSet,
    ChannelMissing,
    SendFailed
}

public readonly record struct WelcomeTestOutcome(
    WelcomeSendResult Result,
    ulong? ChannelId);
