using System.Collections.Concurrent;
using LastRide.Configuration;
using LastRide.Models;
using MongoDB.Driver;

namespace LastRide.Services;

public sealed class PrefixService
{
    public const int MaxPrefixLength = 5;

    private const string DatabaseName = "lastride";
    private const string CollectionName = "guild_prefixes";

    private readonly string _defaultPrefix;
    private readonly IMongoCollection<GuildPrefixDocument>? _collection;
    private readonly ConcurrentDictionary<ulong, string> _cache = new();

    public PrefixService(BotOptions options, MongoDbService mongo)
    {
        _defaultPrefix = options.Prefix;
        _collection = mongo.GetCollection<GuildPrefixDocument>(
            DatabaseName,
            CollectionName);
    }

    public string DefaultPrefix => _defaultPrefix;

    public bool IsPersistent => _collection is not null;

    public async Task LoadAsync()
    {
        if (_collection is null)
            return;

        try
        {
            var documents = await _collection
                .Find(Builders<GuildPrefixDocument>.Filter.Empty)
                .ToListAsync();

            foreach (var document in documents)
            {
                if (ulong.TryParse(document.Id, out var guildId) &&
                    !string.IsNullOrWhiteSpace(document.Prefix))
                {
                    _cache[guildId] = document.Prefix;
                }
            }

            Console.WriteLine(
                $"[Prefix] Loaded {_cache.Count} custom prefix(es) from database.");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Prefix Load Error] {exception}");
        }
    }

    public string GetPrefix(ulong? guildId)
    {
        if (guildId is null)
            return _defaultPrefix;

        return _cache.TryGetValue(guildId.Value, out var prefix)
            ? prefix
            : _defaultPrefix;
    }

    public async Task<bool> SetPrefixAsync(ulong guildId, string prefix)
    {
        _cache[guildId] = prefix;

        if (_collection is null)
            return false;

        try
        {
            var document = new GuildPrefixDocument
            {
                Id = guildId.ToString(),
                Prefix = prefix
            };

            var filter = Builders<GuildPrefixDocument>.Filter.Eq(
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
            Console.WriteLine($"[Prefix Save Error] {exception}");
            return false;
        }
    }

    public async Task<bool> ResetPrefixAsync(ulong guildId)
    {
        _cache.TryRemove(guildId, out _);

        if (_collection is null)
            return false;

        try
        {
            var filter = Builders<GuildPrefixDocument>.Filter.Eq(
                existing => existing.Id,
                guildId.ToString());

            await _collection.DeleteOneAsync(filter);

            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Prefix Reset Error] {exception}");
            return false;
        }
    }
}
