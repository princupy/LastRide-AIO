using System.Collections.Concurrent;
using LastRide.Builders;
using LastRide.Configuration;
using LastRide.Models;
using MongoDB.Driver;

namespace LastRide.Services;

/// <summary>
/// Owns no-prefix access: who holds it, for how long, and the owner check that
/// guards every surface of the feature. A grant applies in every server, so each
/// one is its own document keyed by the member's snowflake. Expiry is resolved
/// lazily on lookup rather than by a background ticker, because the message
/// pipeline is the only thing that ever asks.
/// </summary>
public sealed class NoPrefixService
{
    private const string DatabaseName = "lastride";
    private const string CollectionName = "noprefix";

    /// <summary>Members who may hold no-prefix access at the same time.</summary>
    public const int MaxTrackedUsers = 200;

    private readonly BotOptions _options;
    private readonly IMongoCollection<NoPrefixDocument>? _collection;
    private readonly ConcurrentDictionary<ulong, NoPrefixEntry> _entries = new();

    public NoPrefixService(MongoDbService mongo, BotOptions options)
    {
        _options = options;
        _collection = mongo.GetCollection<NoPrefixDocument>(
            DatabaseName,
            CollectionName);
    }

    public bool IsPersistent => _collection is not null;

    /// <summary>
    /// The single authority for the whole feature. Strictly the configured owner id —
    /// there is no application-owner fallback, so leaving it unset makes every
    /// no-prefix surface dead rather than open.
    /// </summary>
    public bool IsOwner(ulong userId)
    {
        return _options.OwnerId is { } ownerId && userId == ownerId;
    }

    public async Task LoadAsync()
    {
        if (_collection is null)
            return;

        try
        {
            var documents = await _collection
                .Find(Builders<NoPrefixDocument>.Filter.Empty)
                .ToListAsync();

            foreach (var document in documents)
            {
                var entry = FromDocument(document);

                if (entry is null)
                    continue;

                // Already lapsed while the bot was down, so instead of caching the
                // row it gets dropped from the database as well.
                if (entry.HasExpired)
                {
                    await DeleteDocumentAsync(entry.UserId);
                    continue;
                }

                _entries[entry.UserId] = entry;
            }

            Console.WriteLine(
                $"[NoPrefix] Loaded {_entries.Count} no-prefix member(s) from database.");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[NoPrefix Load Error] {exception}");
        }
    }

    /// <summary>
    /// Runs for every message that carries no prefix, so it stays a dictionary
    /// lookup with no awaits. A lapsed grant is dropped here and its row deleted in
    /// the background, which is what keeps expiry working without a ticker.
    /// </summary>
    public bool IsAllowed(ulong userId)
    {
        if (!_entries.TryGetValue(userId, out var entry))
            return false;

        if (!entry.HasExpired)
            return true;

        if (_entries.TryRemove(userId, out _))
            _ = DeleteDocumentAsync(userId);

        return false;
    }

    public NoPrefixEntry? GetEntry(ulong userId)
    {
        return _entries.TryGetValue(userId, out var entry) && !entry.HasExpired
            ? entry
            : null;
    }

    /// <summary>Live grants, newest first — the owner usually wants to see what they just did.</summary>
    public IReadOnlyList<NoPrefixEntry> GetAll()
    {
        return _entries.Values
            .Where(entry => !entry.HasExpired)
            .OrderByDescending(entry => entry.GrantedAt)
            .ToList();
    }

    /// <summary>
    /// Grants access, replacing any existing grant for that member so re-running the
    /// command simply resets the duration.
    /// </summary>
    public async Task<NoPrefixGrantOutcome> GrantAsync(
        ulong userId,
        ulong grantedBy,
        NoPrefixDuration duration)
    {
        if (GetEntry(userId) is null && _entries.Count >= MaxTrackedUsers)
        {
            return new NoPrefixGrantOutcome(
                NoPrefixGrantResult.LimitReached,
                null,
                IsPersistent);
        }

        var now = DateTimeOffset.UtcNow;
        var length = NoPrefixComponentIds.ToTimeSpan(duration);

        var entry = new NoPrefixEntry
        {
            UserId = userId,
            GrantedBy = grantedBy,
            GrantedAt = now.ToUnixTimeSeconds(),
            ExpiresAt = length is { } span
                ? now.Add(span).ToUnixTimeSeconds()
                : 0,
            DurationLabel = NoPrefixComponentIds.ToLabel(duration)
        };

        var persisted = await CommitAsync(entry);

        return new NoPrefixGrantOutcome(
            NoPrefixGrantResult.Granted,
            entry,
            persisted);
    }

    public async Task<NoPrefixRevokeOutcome> RevokeAsync(ulong userId)
    {
        if (!_entries.TryRemove(userId, out _))
        {
            return new NoPrefixRevokeOutcome(
                NoPrefixRevokeResult.NotFound,
                IsPersistent);
        }

        await DeleteDocumentAsync(userId);

        return new NoPrefixRevokeOutcome(
            NoPrefixRevokeResult.Done,
            IsPersistent);
    }

    private async Task<bool> CommitAsync(NoPrefixEntry entry)
    {
        _entries[entry.UserId] = entry;

        if (_collection is null)
            return false;

        try
        {
            var document = ToDocument(entry);

            var filter = Builders<NoPrefixDocument>.Filter.Eq(
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
            Console.WriteLine($"[NoPrefix Save Error] {exception}");
            return false;
        }
    }

    private async Task DeleteDocumentAsync(ulong userId)
    {
        if (_collection is null)
            return;

        try
        {
            var filter = Builders<NoPrefixDocument>.Filter.Eq(
                existing => existing.Id,
                userId.ToString());

            await _collection.DeleteOneAsync(filter);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[NoPrefix Delete Error] {exception}");
        }
    }

    private static NoPrefixDocument ToDocument(NoPrefixEntry entry)
    {
        return new NoPrefixDocument
        {
            Id = entry.UserId.ToString(),
            UserId = entry.UserId.ToString(),
            GrantedBy = entry.GrantedBy.ToString(),
            GrantedAt = entry.GrantedAt,
            ExpiresAt = entry.ExpiresAt,
            DurationLabel = entry.DurationLabel
        };
    }

    private static NoPrefixEntry? FromDocument(NoPrefixDocument document)
    {
        if (!ulong.TryParse(document.Id, out var userId))
            return null;

        _ = ulong.TryParse(document.GrantedBy, out var grantedBy);

        return new NoPrefixEntry
        {
            UserId = userId,
            GrantedBy = grantedBy,
            GrantedAt = document.GrantedAt,
            ExpiresAt = document.ExpiresAt,
            DurationLabel = string.IsNullOrWhiteSpace(document.DurationLabel)
                ? "Unknown"
                : document.DurationLabel
        };
    }
}

public enum NoPrefixGrantResult
{
    Granted,
    LimitReached
}

public enum NoPrefixRevokeResult
{
    Done,
    NotFound
}

public readonly record struct NoPrefixGrantOutcome(
    NoPrefixGrantResult Result,
    NoPrefixEntry? Entry,
    bool Persisted);

public readonly record struct NoPrefixRevokeOutcome(
    NoPrefixRevokeResult Result,
    bool Persisted);
