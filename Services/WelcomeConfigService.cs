using System.Collections.Concurrent;
using LastRide.Models;
using MongoDB.Driver;

namespace LastRide.Services;

public sealed class WelcomeConfigService
{
    // Keeps the rendered greeting comfortably inside Discord's message limit
    // once the placeholders expand into mentions and the server name.
    public const int MaxMessageLength = 500;

    private const string DatabaseName = "lastride";
    private const string CollectionName = "welcome_configs";

    private static readonly WelcomeConfig EmptyConfig = new();

    private readonly IMongoCollection<WelcomeConfigDocument>? _collection;
    private readonly ConcurrentDictionary<ulong, WelcomeConfig> _cache = new();

    public WelcomeConfigService(MongoDbService mongo)
    {
        _collection = mongo.GetCollection<WelcomeConfigDocument>(
            DatabaseName,
            CollectionName);
    }

    public bool IsPersistent => _collection is not null;

    public async Task LoadAsync()
    {
        if (_collection is null)
            return;

        try
        {
            var documents = await _collection
                .Find(Builders<WelcomeConfigDocument>.Filter.Empty)
                .ToListAsync();

            foreach (var document in documents)
            {
                var config = FromDocument(document);

                if (config is not null)
                    _cache[config.GuildId] = config;
            }

            Console.WriteLine(
                $"[Welcome] Loaded {_cache.Count} guild config(s) from database.");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Welcome Load Error] {exception}");
        }
    }

    public WelcomeConfig GetConfig(ulong guildId)
    {
        return _cache.TryGetValue(guildId, out var config)
            ? config
            : EmptyConfig;
    }

    public async Task<bool> SetEnabledAsync(ulong guildId, bool enabled)
    {
        var config = GetOrCreateClone(guildId);
        config.Enabled = enabled;
        return await CommitAsync(guildId, config);
    }

    public async Task<bool> SetChannelAsync(ulong guildId, ulong? channelId)
    {
        var config = GetOrCreateClone(guildId);
        config.ChannelId = channelId;
        return await CommitAsync(guildId, config);
    }

    public async Task<bool> SetMessageAsync(ulong guildId, string? message)
    {
        var config = GetOrCreateClone(guildId);

        // A blank template falls back to the built-in greeting rather than
        // posting an empty card.
        config.Message = string.IsNullOrWhiteSpace(message)
            ? null
            : message.Trim();

        return await CommitAsync(guildId, config);
    }

    public async Task<bool> ResetAsync(ulong guildId)
    {
        var config = GetOrCreateClone(guildId);
        config.Enabled = false;
        config.ChannelId = null;
        config.Message = null;
        return await CommitAsync(guildId, config);
    }

    private WelcomeConfig GetOrCreateClone(ulong guildId)
    {
        return _cache.TryGetValue(guildId, out var existing)
            ? existing.Clone()
            : new WelcomeConfig { GuildId = guildId };
    }

    private async Task<bool> CommitAsync(ulong guildId, WelcomeConfig config)
    {
        _cache[guildId] = config;

        if (_collection is null)
            return false;

        try
        {
            var document = ToDocument(config);

            var filter = Builders<WelcomeConfigDocument>.Filter.Eq(
                existing => existing.Id,
                document.Id);

            await _collection.ReplaceOneAsync(
                filter,
                document,
                new ReplaceOptions { IsUpsert = true });

            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Welcome Save Error] {exception}");
            return false;
        }
    }

    private static WelcomeConfigDocument ToDocument(WelcomeConfig config)
    {
        return new WelcomeConfigDocument
        {
            Id = config.GuildId.ToString(),
            Enabled = config.Enabled,
            ChannelId = config.ChannelId?.ToString(),
            Message = config.Message
        };
    }

    private static WelcomeConfig? FromDocument(WelcomeConfigDocument document)
    {
        if (!ulong.TryParse(document.Id, out var guildId))
            return null;

        var config = new WelcomeConfig
        {
            GuildId = guildId,
            Enabled = document.Enabled,
            Message = string.IsNullOrWhiteSpace(document.Message)
                ? null
                : document.Message
        };

        if (ulong.TryParse(document.ChannelId, out var channelId))
            config.ChannelId = channelId;

        return config;
    }
}
