namespace LastRide.Configuration;

public sealed record BotOptions(
    string Token,
    string Prefix,
    ulong? OwnerId,
    string? MongoConnectionString);
