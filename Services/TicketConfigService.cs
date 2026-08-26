using System.Collections.Concurrent;
using LastRide.Models;
using MongoDB.Driver;

namespace LastRide.Services;

public sealed class TicketConfigService
{
    public const int MaxSupportRoles = 10;
    public const int MaxOpenMessageLength = 1000;
    public const int MaxPanelMessageLength = 1000;
    public const int MinLimit = 1;
    public const int MaxLimit = 10;

    private const string DatabaseName = "lastride";
    private const string CollectionName = "ticket_configs";

    private static readonly TicketConfig DisabledConfig = new();

    private readonly IMongoCollection<TicketConfigDocument>? _collection;
    private readonly ConcurrentDictionary<ulong, TicketConfig> _cache = new();

    public TicketConfigService(MongoDbService mongo)
    {
        _collection = mongo.GetCollection<TicketConfigDocument>(
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
                .Find(Builders<TicketConfigDocument>.Filter.Empty)
                .ToListAsync();

            foreach (var document in documents)
            {
                var config = FromDocument(document);

                if (config is not null)
                    _cache[config.GuildId] = config;
            }

            Console.WriteLine(
                $"[Ticket] Loaded {_cache.Count} guild config(s) from database.");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Ticket Load Error] {exception}");
        }
    }

    public TicketConfig GetConfig(ulong guildId)
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

    public async Task<bool> SetCategoryAsync(ulong guildId, ulong? categoryId)
    {
        var config = GetOrCreateClone(guildId);
        config.CategoryId = categoryId;
        return await CommitAsync(guildId, config);
    }

    public async Task<bool> SetLogChannelAsync(ulong guildId, ulong? channelId)
    {
        var config = GetOrCreateClone(guildId);
        config.LogChannelId = channelId;
        return await CommitAsync(guildId, config);
    }

    public async Task<TicketConfigUpdate> AddSupportRoleAsync(
        ulong guildId,
        ulong roleId)
    {
        var config = GetOrCreateClone(guildId);

        if (config.SupportRoleIds.Contains(roleId))
            return new TicketConfigUpdate(TicketConfigResult.AlreadyPresent, false);

        if (config.SupportRoleIds.Count >= MaxSupportRoles)
            return new TicketConfigUpdate(TicketConfigResult.LimitReached, false);

        config.SupportRoleIds.Add(roleId);
        var persisted = await CommitAsync(guildId, config);

        return new TicketConfigUpdate(TicketConfigResult.Added, persisted);
    }

    public async Task<TicketConfigUpdate> RemoveSupportRoleAsync(
        ulong guildId,
        ulong roleId)
    {
        var config = GetOrCreateClone(guildId);

        if (!config.SupportRoleIds.Remove(roleId))
            return new TicketConfigUpdate(TicketConfigResult.NotPresent, false);

        var persisted = await CommitAsync(guildId, config);

        return new TicketConfigUpdate(TicketConfigResult.Removed, persisted);
    }

    public async Task<bool> SetOpenMessageAsync(ulong guildId, string? message)
    {
        var config = GetOrCreateClone(guildId);

        // A blank template falls back to the built-in text rather than posting
        // an empty card inside the ticket.
        config.OpenMessage = string.IsNullOrWhiteSpace(message)
            ? null
            : message.Trim();

        return await CommitAsync(guildId, config);
    }

    public async Task<bool> SetPanelMessageAsync(ulong guildId, string? message)
    {
        var config = GetOrCreateClone(guildId);

        config.PanelMessage = string.IsNullOrWhiteSpace(message)
            ? null
            : message.Trim();

        return await CommitAsync(guildId, config);
    }

    public async Task<bool> SetLimitAsync(ulong guildId, int limit)
    {
        var config = GetOrCreateClone(guildId);
        config.Limit = Math.Clamp(limit, MinLimit, MaxLimit);
        return await CommitAsync(guildId, config);
    }

    /// <summary>
    /// Hands out the next ticket number and persists the bumped counter, so the
    /// numbering keeps climbing across restarts instead of reusing names.
    /// </summary>
    public async Task<int> NextNumberAsync(ulong guildId)
    {
        var config = GetOrCreateClone(guildId);
        config.Counter++;
        await CommitAsync(guildId, config);
        return config.Counter;
    }

    public async Task<bool> ResetAsync(ulong guildId)
    {
        _cache.TryRemove(guildId, out _);

        if (_collection is null)
            return false;

        try
        {
            var filter = Builders<TicketConfigDocument>.Filter.Eq(
                existing => existing.Id,
                guildId.ToString());

            await _collection.DeleteOneAsync(filter);
            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Ticket Reset Error] {exception}");
            return false;
        }
    }

    private TicketConfig GetOrCreateClone(ulong guildId)
    {
        return _cache.TryGetValue(guildId, out var existing)
            ? existing.Clone()
            : new TicketConfig { GuildId = guildId };
    }

    private async Task<bool> CommitAsync(ulong guildId, TicketConfig config)
    {
        _cache[guildId] = config;

        if (_collection is null)
            return false;

        try
        {
            var document = ToDocument(config);

            var filter = Builders<TicketConfigDocument>.Filter.Eq(
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
            Console.WriteLine($"[Ticket Save Error] {exception}");
            return false;
        }
    }

    private static TicketConfigDocument ToDocument(TicketConfig config)
    {
        return new TicketConfigDocument
        {
            Id = config.GuildId.ToString(),
            Enabled = config.Enabled,
            CategoryId = config.CategoryId?.ToString(),
            LogChannelId = config.LogChannelId?.ToString(),
            SupportRoles = config.SupportRoleIds
                .Select(roleId => roleId.ToString())
                .ToList(),
            OpenMessage = config.OpenMessage,
            PanelMessage = config.PanelMessage,
            Limit = config.Limit,
            Counter = config.Counter
        };
    }

    private static TicketConfig? FromDocument(TicketConfigDocument document)
    {
        if (!ulong.TryParse(document.Id, out var guildId))
            return null;

        var config = new TicketConfig
        {
            GuildId = guildId,
            Enabled = document.Enabled,
            OpenMessage = string.IsNullOrWhiteSpace(document.OpenMessage)
                ? null
                : document.OpenMessage,
            PanelMessage = string.IsNullOrWhiteSpace(document.PanelMessage)
                ? null
                : document.PanelMessage,
            // Documents written before the limit existed carry 0, which would
            // block every ticket, so anything out of range falls back to one.
            Limit = document.Limit is >= MinLimit and <= MaxLimit
                ? document.Limit
                : MinLimit,
            Counter = document.Counter
        };

        if (ulong.TryParse(document.CategoryId, out var categoryId))
            config.CategoryId = categoryId;

        if (ulong.TryParse(document.LogChannelId, out var logChannelId))
            config.LogChannelId = logChannelId;

        foreach (var raw in document.SupportRoles)
        {
            if (ulong.TryParse(raw, out var roleId))
                config.SupportRoleIds.Add(roleId);
        }

        return config;
    }
}

public enum TicketConfigResult
{
    Added,
    Removed,
    AlreadyPresent,
    NotPresent,
    LimitReached
}

public readonly record struct TicketConfigUpdate(
    TicketConfigResult Result,
    bool Persisted);
