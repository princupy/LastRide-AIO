using Discord;
using Discord.WebSocket;
using LastRide.Configuration;

namespace LastRide.Services;

public sealed class BotOwnerService
{
    private readonly DiscordSocketClient _client;
    private readonly BotOptions _options;

    public BotOwnerService(
        DiscordSocketClient client,
        BotOptions options)
    {
        _client = client;
        _options = options;
    }

    public async Task<IUser?> GetOwnerAsync()
    {
        if (_options.OwnerId is { } configuredOwnerId)
            return await _client.GetUserAsync(configuredOwnerId);

        var application = await _client.GetApplicationInfoAsync();

        if (application.Owner is not null)
            return application.Owner;

        if (application.Team is not null)
            return await _client.GetUserAsync(application.Team.OwnerUserId);

        return null;
    }
}
