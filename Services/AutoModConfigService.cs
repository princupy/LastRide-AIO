using System.Collections.Concurrent;
using LastRide.Models;
using MongoDB.Driver;

namespace LastRide.Services;

public sealed class AutoModConfigService
{
    public const int MaxBypassRoles = AutoModDefaults.MaxBypassRoles;
    public const int MaxBadWords = AutoModDefaults.MaxBadWords;

    private const string DatabaseName = "lastride";
    private const string CollectionName = "automod_configs";

    private static readonly AutoModConfig DisabledConfig = new();

    private readonly IMongoCollection<AutoModConfigDocument>? _collection;
    private readonly ConcurrentDictionary<ulong, AutoModConfig> _cache = new();

    public AutoModConfigService(MongoDbService mongo)
    {
        _collection = mongo.GetCollection<AutoModConfigDocument>(
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
                .Find(Builders<AutoModConfigDocument>.Filter.Empty)
                .ToListAsync();

            foreach (var document in documents)
            {
                var config = FromDocument(document);

                if (config is not null)
                    _cache[config.GuildId] = config;
            }

            Console.WriteLine(
                $"[AutoMod] Loaded {_cache.Count} guild config(s) from database.");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[AutoMod Load Error] {exception}");
        }
    }

    public AutoModConfig GetConfig(ulong guildId)
    {
        return _cache.TryGetValue(guildId, out var config)
            ? config
            : DisabledConfig;
    }

    public async Task<bool> SetMasterAsync(ulong guildId, bool enabled)
    {
        var config = GetOrCreateClone(guildId);
        config.Enabled = enabled;
        return await CommitAsync(guildId, config);
    }

    public async Task<bool> SetRuleEnabledAsync(
        ulong guildId,
        AutoModRuleType rule,
        bool enabled)
    {
        var config = GetOrCreateClone(guildId);

        if (!config.Rules.TryGetValue(rule, out var state))
        {
            state = new AutoModRuleState();
            config.Rules[rule] = state;
        }

        state.Enabled = enabled;
        return await CommitAsync(guildId, config);
    }

    public async Task<bool> SetRulesEnabledAsync(
        ulong guildId,
        IReadOnlySet<AutoModRuleType> enabledRules)
    {
        var config = GetOrCreateClone(guildId);

        foreach (var rule in Enum.GetValues<AutoModRuleType>())
        {
            if (!config.Rules.TryGetValue(rule, out var state))
            {
                state = new AutoModRuleState();
                config.Rules[rule] = state;
            }

            state.Enabled = enabledRules.Contains(rule);
        }

        return await CommitAsync(guildId, config);
    }

    public async Task<bool> SetRuleActionAsync(
        ulong guildId,
        AutoModRuleType rule,
        AutoModAction action)
    {
        var config = GetOrCreateClone(guildId);

        if (!config.Rules.TryGetValue(rule, out var state))
        {
            state = new AutoModRuleState();
            config.Rules[rule] = state;
        }

        state.Action = action;
        return await CommitAsync(guildId, config);
    }

    public async Task<BypassRoleUpdate> AddBypassRoleAsync(
        ulong guildId,
        ulong roleId)
    {
        var config = GetOrCreateClone(guildId);

        if (config.BypassRoleIds.Contains(roleId))
            return new BypassRoleUpdate(BypassRoleResult.AlreadyPresent, IsPersistent);

        if (config.BypassRoleIds.Count >= MaxBypassRoles)
            return new BypassRoleUpdate(BypassRoleResult.LimitReached, IsPersistent);

        config.BypassRoleIds.Add(roleId);
        var persisted = await CommitAsync(guildId, config);
        return new BypassRoleUpdate(BypassRoleResult.Added, persisted);
    }

    public async Task<BypassRoleUpdate> RemoveBypassRoleAsync(
        ulong guildId,
        ulong roleId)
    {
        var config = GetOrCreateClone(guildId);

        if (!config.BypassRoleIds.Remove(roleId))
            return new BypassRoleUpdate(BypassRoleResult.NotPresent, IsPersistent);

        var persisted = await CommitAsync(guildId, config);
        return new BypassRoleUpdate(BypassRoleResult.Removed, persisted);
    }

    public async Task<BadWordUpdate> AddBadWordAsync(ulong guildId, string word)
    {
        var normalized = NormalizeBadWord(word);

        if (normalized.Length == 0 ||
            normalized.Length > AutoModDefaults.MaxBadWordLength)
        {
            return new BadWordUpdate(BadWordResult.Invalid, IsPersistent);
        }

        var config = GetOrCreateClone(guildId);

        if (config.BadWords.Contains(normalized))
            return new BadWordUpdate(BadWordResult.AlreadyPresent, IsPersistent);

        if (config.BadWords.Count >= MaxBadWords)
            return new BadWordUpdate(BadWordResult.LimitReached, IsPersistent);

        config.BadWords.Add(normalized);
        var persisted = await CommitAsync(guildId, config);
        return new BadWordUpdate(BadWordResult.Added, persisted);
    }

    public async Task<BadWordUpdate> RemoveBadWordAsync(ulong guildId, string word)
    {
        var normalized = NormalizeBadWord(word);

        if (normalized.Length == 0)
            return new BadWordUpdate(BadWordResult.Invalid, IsPersistent);

        var config = GetOrCreateClone(guildId);

        if (!config.BadWords.Remove(normalized))
            return new BadWordUpdate(BadWordResult.NotPresent, IsPersistent);

        var persisted = await CommitAsync(guildId, config);
        return new BadWordUpdate(BadWordResult.Removed, persisted);
    }

    private static string NormalizeBadWord(string word)
    {
        return word.Trim().ToLowerInvariant();
    }

    public async Task<bool> SetLogChannelAsync(ulong guildId, ulong? channelId)
    {
        var config = GetOrCreateClone(guildId);
        config.LogChannelId = channelId;
        return await CommitAsync(guildId, config);
    }

    private AutoModConfig GetOrCreateClone(ulong guildId)
    {
        return _cache.TryGetValue(guildId, out var existing)
            ? existing.Clone()
            : new AutoModConfig { GuildId = guildId };
    }

    private async Task<bool> CommitAsync(ulong guildId, AutoModConfig config)
    {
        _cache[guildId] = config;

        if (_collection is null)
            return false;

        try
        {
            var document = ToDocument(config);

            var filter = Builders<AutoModConfigDocument>.Filter.Eq(
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
            Console.WriteLine($"[AutoMod Save Error] {exception}");
            return false;
        }
    }

    private static AutoModConfigDocument ToDocument(AutoModConfig config)
    {
        return new AutoModConfigDocument
        {
            Id = config.GuildId.ToString(),
            Enabled = config.Enabled,
            LogChannelId = config.LogChannelId?.ToString(),
            BypassRoleIds = config.BypassRoleIds
                .Select(id => id.ToString())
                .ToList(),
            BadWords = config.BadWords.ToList(),
            Rules = config.Rules
                .Select(pair => new AutoModRuleEntry
                {
                    Type = pair.Key.ToString(),
                    Enabled = pair.Value.Enabled,
                    Action = pair.Value.Action.ToStorage()
                })
                .ToList()
        };
    }

    private static AutoModConfig? FromDocument(AutoModConfigDocument document)
    {
        if (!ulong.TryParse(document.Id, out var guildId))
            return null;

        var config = new AutoModConfig
        {
            GuildId = guildId,
            Enabled = document.Enabled
        };

        if (ulong.TryParse(document.LogChannelId, out var logChannelId))
            config.LogChannelId = logChannelId;

        foreach (var roleId in document.BypassRoleIds)
        {
            if (ulong.TryParse(roleId, out var parsedRoleId))
                config.BypassRoleIds.Add(parsedRoleId);
        }

        foreach (var word in document.BadWords)
        {
            var normalized = NormalizeBadWord(word);

            if (normalized.Length > 0)
                config.BadWords.Add(normalized);
        }

        foreach (var entry in document.Rules)
        {
            if (!Enum.TryParse<AutoModRuleType>(entry.Type, out var rule))
                continue;

            AutoModActionExtensions.TryParse(entry.Action, out var action);

            config.Rules[rule] = new AutoModRuleState
            {
                Enabled = entry.Enabled,
                Action = action
            };
        }

        return config;
    }
}

public enum BypassRoleResult
{
    Added,
    AlreadyPresent,
    Removed,
    NotPresent,
    LimitReached
}

public readonly record struct BypassRoleUpdate(
    BypassRoleResult Result,
    bool Persisted);

public enum BadWordResult
{
    Added,
    AlreadyPresent,
    Removed,
    NotPresent,
    LimitReached,
    Invalid
}

public readonly record struct BadWordUpdate(
    BadWordResult Result,
    bool Persisted);
