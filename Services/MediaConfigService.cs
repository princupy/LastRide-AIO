using System.Collections.Concurrent;
using LastRide.Models;
using MongoDB.Driver;

namespace LastRide.Services;

/// <summary>
/// Owns each guild's media-only settings: which channels are enforced and where
/// removed messages are relayed. Cache-first with a MongoDB write behind it, so
/// the bot keeps working when no connection string is configured.
/// </summary>
public sealed class MediaConfigService
{
    // Comfortably above what a real server needs while keeping the settings card
    // readable and the stored document small.
    public const int MaxChannels = 25;

    private const string DatabaseName = "lastride";
    private const string CollectionName = "media_configs";

    private static readonly MediaConfig EmptyConfig = new();

    private readonly IMongoCollection<MediaConfigDocument>? _collection;
    private readonly ConcurrentDictionary<ulong, MediaConfig> _cache = new();

    public MediaConfigService(MongoDbService mongo)
    {
        _collection = mongo.GetCollection<MediaConfigDocument>(
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
                .Find(Builders<MediaConfigDocument>.Filter.Empty)
                .ToListAsync();

            foreach (var document in documents)
            {
                var config = FromDocument(document);

                if (config is not null)
                    _cache[config.GuildId] = config;
            }

            Console.WriteLine(
                $"[Media] Loaded {_cache.Count} guild config(s) from database.");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Media Load Error] {exception}");
        }
    }

    public MediaConfig GetConfig(ulong guildId)
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

    public async Task<bool> SetChatChannelAsync(ulong guildId, ulong? channelId)
    {
        var config = GetOrCreateClone(guildId);
        config.ChatChannelId = channelId;
        return await CommitAsync(guildId, config);
    }

    /// <summary>
    /// Adds a whole batch in one commit so `media setup #a #b #c` costs a single
    /// database write instead of one per channel.
    /// </summary>
    public async Task<MediaChannelUpdate> AddChannelsAsync(
        ulong guildId,
        IReadOnlyCollection<ulong> channelIds,
        bool enable)
    {
        var config = GetOrCreateClone(guildId);
        var changed = 0;
        var skipped = 0;
        var limitReached = false;

        foreach (var channelId in channelIds)
        {
            if (config.ChannelIds.Contains(channelId))
            {
                skipped++;
                continue;
            }

            if (config.ChannelIds.Count >= MaxChannels)
            {
                limitReached = true;
                break;
            }

            config.ChannelIds.Add(channelId);
            changed++;
        }

        // Nothing moved, so skip the write entirely — a no-op command should
        // never touch the database.
        if (changed == 0)
            return new MediaChannelUpdate(0, skipped, limitReached, IsPersistent);

        // Enforcement is switched on with the first channel: adding one and
        // seeing nothing happen would be the more confusing default.
        if (enable)
            config.Enabled = true;

        var persisted = await CommitAsync(guildId, config);
        return new MediaChannelUpdate(changed, skipped, limitReached, persisted);
    }

    public async Task<MediaChannelUpdate> RemoveChannelsAsync(
        ulong guildId,
        IReadOnlyCollection<ulong> channelIds)
    {
        var config = GetOrCreateClone(guildId);
        var changed = 0;
        var skipped = 0;

        foreach (var channelId in channelIds)
        {
            if (config.ChannelIds.Remove(channelId))
            {
                changed++;
            }
            else
            {
                skipped++;
            }
        }

        if (changed == 0)
            return new MediaChannelUpdate(0, skipped, false, IsPersistent);

        var persisted = await CommitAsync(guildId, config);
        return new MediaChannelUpdate(changed, skipped, false, persisted);
    }

    public async Task<bool> ResetAsync(ulong guildId)
    {
        var config = GetOrCreateClone(guildId);
        config.Enabled = false;
        config.ChatChannelId = null;
        config.ChannelIds.Clear();
        return await CommitAsync(guildId, config);
    }

    private MediaConfig GetOrCreateClone(ulong guildId)
    {
        return _cache.TryGetValue(guildId, out var existing)
            ? existing.Clone()
            : new MediaConfig { GuildId = guildId };
    }

    private async Task<bool> CommitAsync(ulong guildId, MediaConfig config)
    {
        _cache[guildId] = config;

        if (_collection is null)
            return false;

        try
        {
            var document = ToDocument(config);

            var filter = Builders<MediaConfigDocument>.Filter.Eq(
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
            Console.WriteLine($"[Media Save Error] {exception}");
            return false;
        }
    }

    private static MediaConfigDocument ToDocument(MediaConfig config)
    {
        return new MediaConfigDocument
        {
            Id = config.GuildId.ToString(),
            Enabled = config.Enabled,
            ChatChannel = config.ChatChannelId?.ToString(),
            Channels = config
                .ChannelIds
                .Select(channelId => channelId.ToString())
                .ToList()
        };
    }

    private static MediaConfig? FromDocument(MediaConfigDocument document)
    {
        if (!ulong.TryParse(document.Id, out var guildId))
            return null;

        var config = new MediaConfig
        {
            GuildId = guildId,
            Enabled = document.Enabled
        };

        if (ulong.TryParse(document.ChatChannel, out var chatChannelId))
            config.ChatChannelId = chatChannelId;

        foreach (var channelId in document.Channels)
        {
            if (ulong.TryParse(channelId, out var parsedChannelId))
                config.ChannelIds.Add(parsedChannelId);
        }

        return config;
    }
}

public readonly record struct MediaChannelUpdate(
    int Changed,
    int Skipped,
    bool LimitReached,
    bool Persisted);
