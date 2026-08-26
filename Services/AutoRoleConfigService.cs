using System.Collections.Concurrent;
using LastRide.Models;
using MongoDB.Driver;

namespace LastRide.Services;

public sealed class AutoRoleConfigService
{
    // Per-list cap (humans and bots each). Keeps a single join from triggering an
    // unbounded burst of role assignments.
    public const int MaxRolesPerType = 10;

    private const string DatabaseName = "lastride";
    private const string CollectionName = "autorole_configs";

    private static readonly AutoRoleConfig DisabledConfig = new();

    private readonly IMongoCollection<AutoRoleConfigDocument>? _collection;
    private readonly ConcurrentDictionary<ulong, AutoRoleConfig> _cache = new();

    public AutoRoleConfigService(MongoDbService mongo)
    {
        _collection = mongo.GetCollection<AutoRoleConfigDocument>(
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
                .Find(Builders<AutoRoleConfigDocument>.Filter.Empty)
                .ToListAsync();

            foreach (var document in documents)
            {
                var config = FromDocument(document);

                if (config is not null)
                    _cache[config.GuildId] = config;
            }

            Console.WriteLine(
                $"[AutoRole] Loaded {_cache.Count} guild config(s) from database.");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[AutoRole Load Error] {exception}");
        }
    }

    public AutoRoleConfig GetConfig(ulong guildId)
    {
        return _cache.TryGetValue(guildId, out var config)
            ? config
            : DisabledConfig;
    }

    public async Task<bool> SetAutoRoleEnabledAsync(ulong guildId, bool enabled)
    {
        var config = GetOrCreateClone(guildId);
        config.AutoRoleEnabled = enabled;
        return await CommitAsync(guildId, config);
    }

    public async Task<RoleListUpdate> AddAutoRoleAsync(
        ulong guildId,
        ulong roleId,
        AutoRoleTarget target)
    {
        var config = GetOrCreateClone(guildId);
        var buckets = SelectBuckets(config, target);

        // A targeted list that is already full (and doesn't hold the role) blocks the add.
        if (buckets.Any(bucket => !bucket.Contains(roleId) && bucket.Count >= MaxRolesPerType))
            return new RoleListUpdate(RoleListResult.LimitReached, IsPersistent);

        var addedAny = false;

        foreach (var bucket in buckets)
        {
            if (bucket.Add(roleId))
                addedAny = true;
        }

        if (!addedAny)
            return new RoleListUpdate(RoleListResult.AlreadyPresent, IsPersistent);

        var persisted = await CommitAsync(guildId, config);
        return new RoleListUpdate(RoleListResult.Added, persisted);
    }

    public async Task<RoleListUpdate> RemoveAutoRoleAsync(ulong guildId, ulong roleId)
    {
        var config = GetOrCreateClone(guildId);

        var removed = config.HumanRoleIds.Remove(roleId);
        removed |= config.BotRoleIds.Remove(roleId);

        if (!removed)
            return new RoleListUpdate(RoleListResult.NotPresent, IsPersistent);

        var persisted = await CommitAsync(guildId, config);
        return new RoleListUpdate(RoleListResult.Removed, persisted);
    }

    public async Task<bool> ResetAutoRolesAsync(ulong guildId)
    {
        var config = GetOrCreateClone(guildId);
        config.AutoRoleEnabled = false;
        config.HumanRoleIds.Clear();
        config.BotRoleIds.Clear();
        return await CommitAsync(guildId, config);
    }

    public async Task<bool> SetVcRoleEnabledAsync(ulong guildId, bool enabled)
    {
        var config = GetOrCreateClone(guildId);
        config.VcRoleEnabled = enabled;
        return await CommitAsync(guildId, config);
    }

    public async Task<bool> SetVcRoleAsync(ulong guildId, ulong? roleId)
    {
        var config = GetOrCreateClone(guildId);
        config.VcRoleId = roleId;
        return await CommitAsync(guildId, config);
    }

    public async Task<bool> ResetVcRoleAsync(ulong guildId)
    {
        var config = GetOrCreateClone(guildId);
        config.VcRoleEnabled = false;
        config.VcRoleId = null;
        return await CommitAsync(guildId, config);
    }

    private static List<HashSet<ulong>> SelectBuckets(
        AutoRoleConfig config,
        AutoRoleTarget target)
    {
        return target switch
        {
            AutoRoleTarget.Humans => new List<HashSet<ulong>> { config.HumanRoleIds },
            AutoRoleTarget.Bots => new List<HashSet<ulong>> { config.BotRoleIds },
            _ => new List<HashSet<ulong>> { config.HumanRoleIds, config.BotRoleIds }
        };
    }

    private AutoRoleConfig GetOrCreateClone(ulong guildId)
    {
        return _cache.TryGetValue(guildId, out var existing)
            ? existing.Clone()
            : new AutoRoleConfig { GuildId = guildId };
    }

    private async Task<bool> CommitAsync(ulong guildId, AutoRoleConfig config)
    {
        _cache[guildId] = config;

        if (_collection is null)
            return false;

        try
        {
            var document = ToDocument(config);

            var filter = Builders<AutoRoleConfigDocument>.Filter.Eq(
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
            Console.WriteLine($"[AutoRole Save Error] {exception}");
            return false;
        }
    }

    private static AutoRoleConfigDocument ToDocument(AutoRoleConfig config)
    {
        return new AutoRoleConfigDocument
        {
            Id = config.GuildId.ToString(),
            AutoRoleEnabled = config.AutoRoleEnabled,
            HumanRoleIds = config.HumanRoleIds.Select(id => id.ToString()).ToList(),
            BotRoleIds = config.BotRoleIds.Select(id => id.ToString()).ToList(),
            VcRoleEnabled = config.VcRoleEnabled,
            VcRoleId = config.VcRoleId?.ToString()
        };
    }

    private static AutoRoleConfig? FromDocument(AutoRoleConfigDocument document)
    {
        if (!ulong.TryParse(document.Id, out var guildId))
            return null;

        var config = new AutoRoleConfig
        {
            GuildId = guildId,
            AutoRoleEnabled = document.AutoRoleEnabled,
            VcRoleEnabled = document.VcRoleEnabled
        };

        foreach (var roleId in document.HumanRoleIds)
        {
            if (ulong.TryParse(roleId, out var parsedRoleId))
                config.HumanRoleIds.Add(parsedRoleId);
        }

        foreach (var roleId in document.BotRoleIds)
        {
            if (ulong.TryParse(roleId, out var parsedRoleId))
                config.BotRoleIds.Add(parsedRoleId);
        }

        if (ulong.TryParse(document.VcRoleId, out var vcRoleId))
            config.VcRoleId = vcRoleId;

        return config;
    }
}

public enum AutoRoleTarget
{
    All,
    Humans,
    Bots
}

public enum RoleListResult
{
    Added,
    AlreadyPresent,
    Removed,
    NotPresent,
    LimitReached
}

public readonly record struct RoleListUpdate(
    RoleListResult Result,
    bool Persisted);
