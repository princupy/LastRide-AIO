using System.Collections.Concurrent;
using LastRide.Models;
using MongoDB.Driver;

namespace LastRide.Services;

public sealed class LogConfigService
{
    private const string DatabaseName = "lastride";
    private const string CollectionName = "log_configs";

    private static readonly LogConfig DisabledConfig = new();

    private readonly IMongoCollection<LogConfigDocument>? _collection;
    private readonly ConcurrentDictionary<ulong, LogConfig> _cache = new();

    public LogConfigService(MongoDbService mongo)
    {
        _collection = mongo.GetCollection<LogConfigDocument>(
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
                .Find(Builders<LogConfigDocument>.Filter.Empty)
                .ToListAsync();

            foreach (var document in documents)
            {
                var config = FromDocument(document);

                if (config is not null)
                    _cache[config.GuildId] = config;
            }

            Console.WriteLine(
                $"[Logs] Loaded {_cache.Count} guild config(s) from database.");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Logs Load Error] {exception}");
        }
    }

    public LogConfig GetConfig(ulong guildId)
    {
        return _cache.TryGetValue(guildId, out var config)
            ? config
            : DisabledConfig;
    }

    public async Task<bool> SetEnabledAsync(ulong guildId, bool enabled)
    {
        var config = GetOrCreateClone(guildId);
        config.Enabled = enabled;
        return await CommitAsync(guildId, config);
    }

    public async Task<bool> SetChannelAsync(
        ulong guildId,
        LogType type,
        ulong? channelId)
    {
        var config = GetOrCreateClone(guildId);

        if (channelId is { } id)
        {
            config.Channels[type] = id;

            // Setting the first channel auto-enables the master switch so the
            // type starts logging immediately without a separate enable step.
            config.Enabled = true;
        }
        else
        {
            config.Channels.Remove(type);
        }

        return await CommitAsync(guildId, config);
    }

    public async Task<bool> ResetAsync(ulong guildId)
    {
        _cache.TryRemove(guildId, out _);

        if (_collection is null)
            return false;

        try
        {
            var filter = Builders<LogConfigDocument>.Filter.Eq(
                existing => existing.Id,
                guildId.ToString());

            await _collection.DeleteOneAsync(filter);

            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Logs Reset Error] {exception}");
            return false;
        }
    }

    private LogConfig GetOrCreateClone(ulong guildId)
    {
        return _cache.TryGetValue(guildId, out var existing)
            ? existing.Clone()
            : new LogConfig { GuildId = guildId };
    }

    private async Task<bool> CommitAsync(ulong guildId, LogConfig config)
    {
        _cache[guildId] = config;

        if (_collection is null)
            return false;

        try
        {
            var document = ToDocument(config);

            var filter = Builders<LogConfigDocument>.Filter.Eq(
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
            Console.WriteLine($"[Logs Save Error] {exception}");
            return false;
        }
    }

    private static LogConfigDocument ToDocument(LogConfig config)
    {
        return new LogConfigDocument
        {
            Id = config.GuildId.ToString(),
            Enabled = config.Enabled,
            Channels = config.Channels
                .Select(pair => new LogChannelEntry
                {
                    Type = pair.Key.ToString(),
                    ChannelId = pair.Value.ToString()
                })
                .ToList()
        };
    }

    private static LogConfig? FromDocument(LogConfigDocument document)
    {
        if (!ulong.TryParse(document.Id, out var guildId))
            return null;

        var config = new LogConfig
        {
            GuildId = guildId,
            Enabled = document.Enabled
        };

        foreach (var entry in document.Channels)
        {
            if (!Enum.TryParse<LogType>(entry.Type, out var type))
                continue;

            if (ulong.TryParse(entry.ChannelId, out var channelId))
                config.Channels[type] = channelId;
        }

        return config;
    }
}
