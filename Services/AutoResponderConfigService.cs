using System.Collections.Concurrent;
using LastRide.Models;
using MongoDB.Driver;

namespace LastRide.Services;

public sealed class AutoResponderConfigService
{
    // Caps keep a single guild's responder set (and each entry) bounded so the
    // list card stays readable and a match can't post an oversized reply.
    public const int MaxResponders = 30;
    public const int MaxTriggerLength = 100;
    public const int MaxReplyLength = 1500;

    private const string DatabaseName = "lastride";
    private const string CollectionName = "autoresponder_configs";

    private static readonly AutoResponderConfig EmptyConfig = new();

    private readonly IMongoCollection<AutoResponderConfigDocument>? _collection;
    private readonly ConcurrentDictionary<ulong, AutoResponderConfig> _cache = new();

    public AutoResponderConfigService(MongoDbService mongo)
    {
        _collection = mongo.GetCollection<AutoResponderConfigDocument>(
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
                .Find(Builders<AutoResponderConfigDocument>.Filter.Empty)
                .ToListAsync();

            foreach (var document in documents)
            {
                var config = FromDocument(document);

                if (config is not null)
                    _cache[config.GuildId] = config;
            }

            Console.WriteLine(
                $"[AutoResponder] Loaded {_cache.Count} guild config(s) from database.");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[AutoResponder Load Error] {exception}");
        }
    }

    public AutoResponderConfig GetConfig(ulong guildId)
    {
        return _cache.TryGetValue(guildId, out var config)
            ? config
            : EmptyConfig;
    }

    public async Task<ResponderUpdate> AddResponseAsync(
        ulong guildId,
        string trigger,
        string reply)
    {
        var config = GetOrCreateClone(guildId);
        var exists = config.Responses.ContainsKey(trigger);

        // A full list only blocks brand-new triggers; editing an existing
        // trigger's reply is always allowed.
        if (!exists && config.Responses.Count >= MaxResponders)
            return new ResponderUpdate(ResponderResult.LimitReached, IsPersistent);

        config.Responses[trigger] = reply;

        var persisted = await CommitAsync(guildId, config);

        return new ResponderUpdate(
            exists ? ResponderResult.Updated : ResponderResult.Added,
            persisted);
    }

    public async Task<ResponderUpdate> EditResponseAsync(
        ulong guildId,
        string trigger,
        string reply)
    {
        var config = GetOrCreateClone(guildId);

        // Edit only touches a trigger that already exists — a missing trigger
        // tells the caller to use `add` instead of silently creating one.
        if (!config.Responses.ContainsKey(trigger))
            return new ResponderUpdate(ResponderResult.NotPresent, IsPersistent);

        config.Responses[trigger] = reply;

        var persisted = await CommitAsync(guildId, config);
        return new ResponderUpdate(ResponderResult.Updated, persisted);
    }

    public async Task<ResponderUpdate> RemoveResponseAsync(
        ulong guildId,
        string trigger)
    {
        var config = GetOrCreateClone(guildId);

        if (!config.Responses.Remove(trigger))
            return new ResponderUpdate(ResponderResult.NotPresent, IsPersistent);

        var persisted = await CommitAsync(guildId, config);
        return new ResponderUpdate(ResponderResult.Removed, persisted);
    }

    private AutoResponderConfig GetOrCreateClone(ulong guildId)
    {
        return _cache.TryGetValue(guildId, out var existing)
            ? existing.Clone()
            : new AutoResponderConfig { GuildId = guildId };
    }

    private async Task<bool> CommitAsync(ulong guildId, AutoResponderConfig config)
    {
        _cache[guildId] = config;

        if (_collection is null)
            return false;

        try
        {
            var document = ToDocument(config);

            var filter = Builders<AutoResponderConfigDocument>.Filter.Eq(
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
            Console.WriteLine($"[AutoResponder Save Error] {exception}");
            return false;
        }
    }

    private static AutoResponderConfigDocument ToDocument(AutoResponderConfig config)
    {
        return new AutoResponderConfigDocument
        {
            Id = config.GuildId.ToString(),
            Responses = config.Responses
                .Select(pair => new AutoResponderEntryDocument
                {
                    Trigger = pair.Key,
                    Reply = pair.Value
                })
                .ToList()
        };
    }

    private static AutoResponderConfig? FromDocument(AutoResponderConfigDocument document)
    {
        if (!ulong.TryParse(document.Id, out var guildId))
            return null;

        var config = new AutoResponderConfig { GuildId = guildId };

        foreach (var entry in document.Responses)
        {
            if (!string.IsNullOrWhiteSpace(entry.Trigger))
                config.Responses[entry.Trigger] = entry.Reply ?? string.Empty;
        }

        return config;
    }
}

public enum ResponderResult
{
    Added,
    Updated,
    Removed,
    NotPresent,
    LimitReached
}

public readonly record struct ResponderUpdate(
    ResponderResult Result,
    bool Persisted);
