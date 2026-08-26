using System.Collections.Concurrent;
using LastRide.Models;
using MongoDB.Driver;

namespace LastRide.Services;

public sealed class LevelConfigService
{
    public const int MaxLevelRoles = LevelDefaults.MaxLevelRoles;
    public const int MaxBlacklistedChannels = LevelDefaults.MaxBlacklistedChannels;
    public const int MaxBlacklistedRoles = LevelDefaults.MaxBlacklistedRoles;

    private const string DatabaseName = "lastride";
    private const string CollectionName = "level_configs";

    private static readonly LevelConfig DisabledConfig = new();

    private readonly IMongoCollection<LevelConfigDocument>? _collection;
    private readonly ConcurrentDictionary<ulong, LevelConfig> _cache = new();

    public LevelConfigService(MongoDbService mongo)
    {
        _collection = mongo.GetCollection<LevelConfigDocument>(
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
                .Find(Builders<LevelConfigDocument>.Filter.Empty)
                .ToListAsync();

            foreach (var document in documents)
            {
                var config = FromDocument(document);

                if (config is not null)
                    _cache[config.GuildId] = config;
            }

            Console.WriteLine(
                $"[Leveling] Loaded {_cache.Count} guild config(s) from database.");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Leveling Load Error] {exception}");
        }
    }

    public LevelConfig GetConfig(ulong guildId)
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

    public async Task<bool> SetCooldownAsync(ulong guildId, int seconds)
    {
        var config = GetOrCreateClone(guildId);
        config.XpCooldownSeconds = Math.Clamp(
            seconds,
            LevelDefaults.MinCooldownSeconds,
            LevelDefaults.MaxCooldownSeconds);
        return await CommitAsync(guildId, config);
    }

    public async Task<bool> SetXpRateAsync(ulong guildId, int minimum, int maximum)
    {
        var config = GetOrCreateClone(guildId);
        config.MinXpPerMessage = minimum;
        config.MaxXpPerMessage = maximum;
        return await CommitAsync(guildId, config);
    }

    public async Task<bool> SetLevelUpAnnouncementsAsync(ulong guildId, bool enabled)
    {
        var config = GetOrCreateClone(guildId);
        config.LevelUpAnnouncementsEnabled = enabled;
        return await CommitAsync(guildId, config);
    }

    public async Task<bool> SetLevelUpChannelAsync(ulong guildId, ulong? channelId)
    {
        var config = GetOrCreateClone(guildId);
        config.LevelUpChannelId = channelId;
        return await CommitAsync(guildId, config);
    }

    public async Task<bool> SetLevelUpMessageAsync(ulong guildId, string? message)
    {
        var config = GetOrCreateClone(guildId);
        config.LevelUpMessage = string.IsNullOrWhiteSpace(message)
            ? null
            : message.Trim();
        return await CommitAsync(guildId, config);
    }

    public async Task<bool> SetRoleModeAsync(ulong guildId, LevelRoleMode mode)
    {
        var config = GetOrCreateClone(guildId);
        config.RoleMode = mode;
        return await CommitAsync(guildId, config);
    }

    public async Task<LevelListUpdate> AddBlacklistChannelAsync(
        ulong guildId,
        ulong channelId)
    {
        var config = GetOrCreateClone(guildId);

        if (config.BlacklistedChannelIds.Contains(channelId))
            return new LevelListUpdate(LevelListResult.AlreadyPresent, IsPersistent);

        if (config.BlacklistedChannelIds.Count >= MaxBlacklistedChannels)
            return new LevelListUpdate(LevelListResult.LimitReached, IsPersistent);

        config.BlacklistedChannelIds.Add(channelId);
        var persisted = await CommitAsync(guildId, config);
        return new LevelListUpdate(LevelListResult.Added, persisted);
    }

    public async Task<LevelListUpdate> RemoveBlacklistChannelAsync(
        ulong guildId,
        ulong channelId)
    {
        var config = GetOrCreateClone(guildId);

        if (!config.BlacklistedChannelIds.Remove(channelId))
            return new LevelListUpdate(LevelListResult.NotPresent, IsPersistent);

        var persisted = await CommitAsync(guildId, config);
        return new LevelListUpdate(LevelListResult.Removed, persisted);
    }

    public async Task<LevelListUpdate> AddBlacklistRoleAsync(
        ulong guildId,
        ulong roleId)
    {
        var config = GetOrCreateClone(guildId);

        if (config.BlacklistedRoleIds.Contains(roleId))
            return new LevelListUpdate(LevelListResult.AlreadyPresent, IsPersistent);

        if (config.BlacklistedRoleIds.Count >= MaxBlacklistedRoles)
            return new LevelListUpdate(LevelListResult.LimitReached, IsPersistent);

        config.BlacklistedRoleIds.Add(roleId);
        var persisted = await CommitAsync(guildId, config);
        return new LevelListUpdate(LevelListResult.Added, persisted);
    }

    public async Task<LevelListUpdate> RemoveBlacklistRoleAsync(
        ulong guildId,
        ulong roleId)
    {
        var config = GetOrCreateClone(guildId);

        if (!config.BlacklistedRoleIds.Remove(roleId))
            return new LevelListUpdate(LevelListResult.NotPresent, IsPersistent);

        var persisted = await CommitAsync(guildId, config);
        return new LevelListUpdate(LevelListResult.Removed, persisted);
    }

    public async Task<LevelListUpdate> AddLevelRoleAsync(
        ulong guildId,
        int level,
        ulong roleId)
    {
        if (level < 1 || level > LevelDefaults.MaxLevel)
            return new LevelListUpdate(LevelListResult.Invalid, IsPersistent);

        var config = GetOrCreateClone(guildId);

        if (config.LevelRoles.TryGetValue(level, out var existing) &&
            existing == roleId)
        {
            return new LevelListUpdate(LevelListResult.AlreadyPresent, IsPersistent);
        }

        if (!config.LevelRoles.ContainsKey(level) &&
            config.LevelRoles.Count >= MaxLevelRoles)
        {
            return new LevelListUpdate(LevelListResult.LimitReached, IsPersistent);
        }

        config.LevelRoles[level] = roleId;
        var persisted = await CommitAsync(guildId, config);
        return new LevelListUpdate(LevelListResult.Added, persisted);
    }

    public async Task<LevelListUpdate> RemoveLevelRoleByLevelAsync(
        ulong guildId,
        int level)
    {
        var config = GetOrCreateClone(guildId);

        if (!config.LevelRoles.Remove(level))
            return new LevelListUpdate(LevelListResult.NotPresent, IsPersistent);

        var persisted = await CommitAsync(guildId, config);
        return new LevelListUpdate(LevelListResult.Removed, persisted);
    }

    public async Task<LevelListUpdate> RemoveLevelRoleByRoleAsync(
        ulong guildId,
        ulong roleId)
    {
        var config = GetOrCreateClone(guildId);

        var levels = config.LevelRoles
            .Where(pair => pair.Value == roleId)
            .Select(pair => pair.Key)
            .ToArray();

        if (levels.Length == 0)
            return new LevelListUpdate(LevelListResult.NotPresent, IsPersistent);

        foreach (var level in levels)
            config.LevelRoles.Remove(level);

        var persisted = await CommitAsync(guildId, config);
        return new LevelListUpdate(LevelListResult.Removed, persisted);
    }

    public async Task<bool> ResetAsync(ulong guildId)
    {
        _cache.TryRemove(guildId, out _);

        if (_collection is null)
            return false;

        try
        {
            var filter = Builders<LevelConfigDocument>.Filter.Eq(
                document => document.Id,
                guildId.ToString());

            await _collection.DeleteOneAsync(filter);
            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Leveling Save Error] {exception}");
            return false;
        }
    }

    private LevelConfig GetOrCreateClone(ulong guildId)
    {
        return _cache.TryGetValue(guildId, out var existing)
            ? existing.Clone()
            : new LevelConfig { GuildId = guildId };
    }

    private async Task<bool> CommitAsync(ulong guildId, LevelConfig config)
    {
        _cache[guildId] = config;

        if (_collection is null)
            return false;

        try
        {
            var document = ToDocument(config);

            var filter = Builders<LevelConfigDocument>.Filter.Eq(
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
            Console.WriteLine($"[Leveling Save Error] {exception}");
            return false;
        }
    }

    private static LevelConfigDocument ToDocument(LevelConfig config)
    {
        return new LevelConfigDocument
        {
            Id = config.GuildId.ToString(),
            Enabled = config.Enabled,
            XpCooldownSeconds = config.XpCooldownSeconds,
            MinXpPerMessage = config.MinXpPerMessage,
            MaxXpPerMessage = config.MaxXpPerMessage,
            LevelUpAnnouncementsEnabled = config.LevelUpAnnouncementsEnabled,
            LevelUpChannelId = config.LevelUpChannelId?.ToString(),
            LevelUpMessage = config.LevelUpMessage,
            RoleMode = config.RoleMode.ToStorage(),
            BlacklistedChannelIds = config.BlacklistedChannelIds
                .Select(id => id.ToString())
                .ToList(),
            BlacklistedRoleIds = config.BlacklistedRoleIds
                .Select(id => id.ToString())
                .ToList(),
            LevelRoles = config.LevelRoles
                .OrderBy(pair => pair.Key)
                .Select(pair => new LevelRoleEntry
                {
                    Level = pair.Key,
                    RoleId = pair.Value.ToString()
                })
                .ToList()
        };
    }

    private static LevelConfig? FromDocument(LevelConfigDocument document)
    {
        if (!ulong.TryParse(document.Id, out var guildId))
            return null;

        var config = new LevelConfig
        {
            GuildId = guildId,
            Enabled = document.Enabled,
            XpCooldownSeconds = Math.Clamp(
                document.XpCooldownSeconds,
                LevelDefaults.MinCooldownSeconds,
                LevelDefaults.MaxCooldownSeconds),
            LevelUpAnnouncementsEnabled = document.LevelUpAnnouncementsEnabled,
            LevelUpMessage = string.IsNullOrWhiteSpace(document.LevelUpMessage)
                ? null
                : document.LevelUpMessage
        };

        // Guard against a hand-edited document inverting the range.
        var minimum = Math.Clamp(
            document.MinXpPerMessage,
            LevelDefaults.MinXpPerMessage,
            LevelDefaults.MaxXpPerMessage);
        var maximum = Math.Clamp(
            document.MaxXpPerMessage,
            LevelDefaults.MinXpPerMessage,
            LevelDefaults.MaxXpPerMessage);

        config.MinXpPerMessage = Math.Min(minimum, maximum);
        config.MaxXpPerMessage = Math.Max(minimum, maximum);

        if (ulong.TryParse(document.LevelUpChannelId, out var levelUpChannelId))
            config.LevelUpChannelId = levelUpChannelId;

        if (LevelRoleModeExtensions.TryParse(document.RoleMode, out var mode))
            config.RoleMode = mode;

        foreach (var channelId in document.BlacklistedChannelIds)
        {
            if (ulong.TryParse(channelId, out var parsedChannelId))
                config.BlacklistedChannelIds.Add(parsedChannelId);
        }

        foreach (var roleId in document.BlacklistedRoleIds)
        {
            if (ulong.TryParse(roleId, out var parsedRoleId))
                config.BlacklistedRoleIds.Add(parsedRoleId);
        }

        foreach (var entry in document.LevelRoles)
        {
            if (entry.Level < 1 || entry.Level > LevelDefaults.MaxLevel)
                continue;

            if (ulong.TryParse(entry.RoleId, out var parsedRewardRoleId))
                config.LevelRoles[entry.Level] = parsedRewardRoleId;
        }

        return config;
    }
}

public enum LevelListResult
{
    Added,
    AlreadyPresent,
    Removed,
    NotPresent,
    LimitReached,
    Invalid
}

public readonly record struct LevelListUpdate(
    LevelListResult Result,
    bool Persisted);
