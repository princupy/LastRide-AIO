using System.Collections.Concurrent;
using LastRide.Models;
using MongoDB.Driver;

namespace LastRide.Services;

public sealed class SetupRoleConfigService
{
    // Caps keep a single guild's staff allowlist and command set bounded so the
    // list cards stay readable and the dynamic dispatch stays a cheap lookup.
    public const int MaxStaffRoles = 15;
    public const int MaxCommands = 30;
    public const int MinCommandNameLength = 2;
    public const int MaxCommandNameLength = 20;

    private const string DatabaseName = "lastride";
    private const string CollectionName = "setuprole_configs";

    private static readonly SetupRoleConfig EmptyConfig = new();

    private readonly IMongoCollection<SetupRoleConfigDocument>? _collection;
    private readonly ConcurrentDictionary<ulong, SetupRoleConfig> _cache = new();

    public SetupRoleConfigService(MongoDbService mongo)
    {
        _collection = mongo.GetCollection<SetupRoleConfigDocument>(
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
                .Find(Builders<SetupRoleConfigDocument>.Filter.Empty)
                .ToListAsync();

            foreach (var document in documents)
            {
                var config = FromDocument(document);

                if (config is not null)
                    _cache[config.GuildId] = config;
            }

            Console.WriteLine(
                $"[SetupRole] Loaded {_cache.Count} guild config(s) from database.");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[SetupRole Load Error] {exception}");
        }
    }

    public SetupRoleConfig GetConfig(ulong guildId)
    {
        return _cache.TryGetValue(guildId, out var config)
            ? config
            : EmptyConfig;
    }

    public async Task<SetupRoleUpdate> AddStaffRoleAsync(ulong guildId, ulong roleId)
    {
        var config = GetOrCreateClone(guildId);

        if (config.StaffRoleIds.Contains(roleId))
            return new SetupRoleUpdate(SetupRoleResult.AlreadyPresent, IsPersistent);

        if (config.StaffRoleIds.Count >= MaxStaffRoles)
            return new SetupRoleUpdate(SetupRoleResult.LimitReached, IsPersistent);

        config.StaffRoleIds.Add(roleId);

        var persisted = await CommitAsync(guildId, config);
        return new SetupRoleUpdate(SetupRoleResult.Added, persisted);
    }

    public async Task<SetupRoleUpdate> RemoveStaffRoleAsync(ulong guildId, ulong roleId)
    {
        var config = GetOrCreateClone(guildId);

        if (!config.StaffRoleIds.Remove(roleId))
            return new SetupRoleUpdate(SetupRoleResult.NotPresent, IsPersistent);

        var persisted = await CommitAsync(guildId, config);
        return new SetupRoleUpdate(SetupRoleResult.Removed, persisted);
    }

    public async Task<SetupRoleUpdate> SetCommandAsync(
        ulong guildId,
        string name,
        ulong roleId)
    {
        if (!IsValidCommandName(name))
            return new SetupRoleUpdate(SetupRoleResult.Invalid, IsPersistent);

        var config = GetOrCreateClone(guildId);
        var exists = config.Commands.ContainsKey(name);

        // A full list only blocks brand-new commands; repointing an existing
        // command at another role is always allowed.
        if (!exists && config.Commands.Count >= MaxCommands)
            return new SetupRoleUpdate(SetupRoleResult.LimitReached, IsPersistent);

        config.Commands[name] = roleId;

        var persisted = await CommitAsync(guildId, config);

        return new SetupRoleUpdate(
            exists ? SetupRoleResult.Updated : SetupRoleResult.Added,
            persisted);
    }

    public async Task<SetupRoleUpdate> RemoveCommandAsync(ulong guildId, string name)
    {
        var config = GetOrCreateClone(guildId);

        if (!config.Commands.Remove(name))
            return new SetupRoleUpdate(SetupRoleResult.NotPresent, IsPersistent);

        var persisted = await CommitAsync(guildId, config);
        return new SetupRoleUpdate(SetupRoleResult.Removed, persisted);
    }

    // Dynamic names are stored lowercase and stripped of a leading prefix
    // character so `?vip`, `!vip` and `VIP` all normalise to the same entry.
    public static string NormalizeCommandName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var trimmed = name.Trim();

        while (trimmed.Length > 0 && !char.IsLetterOrDigit(trimmed[0]))
            trimmed = trimmed[1..];

        return trimmed.ToLowerInvariant();
    }

    // Names must survive being typed after a prefix, so only characters that
    // never need escaping are allowed.
    public static bool IsValidCommandName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (name.Length < MinCommandNameLength || name.Length > MaxCommandNameLength)
            return false;

        return name.All(character =>
            character is >= 'a' and <= 'z' ||
            character is >= '0' and <= '9' ||
            character is '-' or '_');
    }

    private SetupRoleConfig GetOrCreateClone(ulong guildId)
    {
        return _cache.TryGetValue(guildId, out var existing)
            ? existing.Clone()
            : new SetupRoleConfig { GuildId = guildId };
    }

    private async Task<bool> CommitAsync(ulong guildId, SetupRoleConfig config)
    {
        _cache[guildId] = config;

        if (_collection is null)
            return false;

        try
        {
            var document = ToDocument(config);

            var filter = Builders<SetupRoleConfigDocument>.Filter.Eq(
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
            Console.WriteLine($"[SetupRole Save Error] {exception}");
            return false;
        }
    }

    private static SetupRoleConfigDocument ToDocument(SetupRoleConfig config)
    {
        return new SetupRoleConfigDocument
        {
            Id = config.GuildId.ToString(),
            StaffRoles = config.StaffRoleIds
                .Select(roleId => roleId.ToString())
                .ToList(),
            Commands = config.OrderedCommands
                .Select(pair => new SetupRoleCommandEntry
                {
                    Name = pair.Key,
                    RoleId = pair.Value.ToString()
                })
                .ToList()
        };
    }

    private static SetupRoleConfig? FromDocument(SetupRoleConfigDocument document)
    {
        if (!ulong.TryParse(document.Id, out var guildId))
            return null;

        var config = new SetupRoleConfig { GuildId = guildId };

        foreach (var rawRoleId in document.StaffRoles)
        {
            if (ulong.TryParse(rawRoleId, out var roleId))
                config.StaffRoleIds.Add(roleId);
        }

        foreach (var entry in document.Commands)
        {
            var name = NormalizeCommandName(entry.Name);

            if (IsValidCommandName(name) &&
                ulong.TryParse(entry.RoleId, out var roleId))
            {
                config.Commands[name] = roleId;
            }
        }

        return config;
    }
}

public enum SetupRoleResult
{
    Added,
    Updated,
    Removed,
    AlreadyPresent,
    NotPresent,
    LimitReached,
    Invalid
}

public readonly record struct SetupRoleUpdate(
    SetupRoleResult Result,
    bool Persisted);
