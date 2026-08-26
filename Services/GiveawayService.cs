using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Models;
using MongoDB.Driver;

namespace LastRide.Services;

/// <summary>
/// Owns every giveaway: storage, the entry button, the draw, and the background
/// ticker that ends giveaways once their timer runs out. Giveaways are entities
/// rather than guild settings, so each one is its own document keyed by the
/// snowflake of the message holding its card.
/// </summary>
public sealed class GiveawayService
{
    private const string DatabaseName = "lastride";
    private const string CollectionName = "giveaways";

    /// <summary>Running giveaways one guild may hold at a time.</summary>
    public const int MaxActivePerGuild = 25;

    public const int MaxWinners = 20;
    public const int MaxPrizeLength = 200;

    public static readonly TimeSpan MinDuration = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan MaxDuration = TimeSpan.FromDays(60);

    // Timers live client-side through Discord's relative timestamp, so the ticker
    // only has to notice expiry, flush entry-count edits, and prune old rows.
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);

    /// <summary>How long an ended giveaway stays rerollable before being purged.</summary>
    private const int RetentionDays = 7;

    private readonly DiscordSocketClient _client;
    private readonly GiveawayComponentBuilder _builder;
    private readonly IMongoCollection<GiveawayDocument>? _collection;
    private readonly ConcurrentDictionary<ulong, Giveaway> _giveaways = new();
    private readonly ConcurrentDictionary<ulong, bool> _dirty = new();

    private Task? _tickerTask;

    public GiveawayService(
        DiscordSocketClient client,
        GiveawayComponentBuilder builder,
        MongoDbService mongo)
    {
        _client = client;
        _builder = builder;
        _collection = mongo.GetCollection<GiveawayDocument>(
            DatabaseName,
            CollectionName);
    }

    public bool IsPersistent => _collection is not null;

    public async Task LoadAsync()
    {
        if (_collection is not null)
        {
            try
            {
                var documents = await _collection
                    .Find(Builders<GiveawayDocument>.Filter.Empty)
                    .ToListAsync();

                var cutoff = DateTimeOffset.UtcNow.AddDays(-RetentionDays);

                foreach (var document in documents)
                {
                    var giveaway = FromDocument(document);

                    if (giveaway is null)
                        continue;

                    // Its reroll window has closed, so instead of caching the row it
                    // gets dropped from the database as well.
                    if (giveaway.IsEnded && giveaway.EndsAtUtc <= cutoff)
                    {
                        await DeleteDocumentAsync(giveaway.MessageId);
                        continue;
                    }

                    _giveaways[giveaway.MessageId] = giveaway;
                }

                Console.WriteLine(
                    $"[Giveaway] Loaded {_giveaways.Count} giveaway(s) from database.");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[Giveaway Load Error] {exception}");
            }
        }

        StartTicker();
    }

    public Giveaway? GetGiveaway(ulong messageId)
    {
        return _giveaways.TryGetValue(messageId, out var giveaway) ? giveaway : null;
    }

    /// <summary>Running giveaways, soonest ending first.</summary>
    public IReadOnlyList<Giveaway> GetRunning(ulong guildId)
    {
        return _giveaways.Values
            .Where(giveaway => giveaway.GuildId == guildId && giveaway.IsRunning)
            .OrderBy(giveaway => giveaway.EndsAt)
            .ToList();
    }

    /// <summary>Ended giveaways still inside the reroll window, newest first.</summary>
    public IReadOnlyList<Giveaway> GetEnded(ulong guildId)
    {
        return _giveaways.Values
            .Where(giveaway => giveaway.GuildId == guildId && giveaway.IsEnded)
            .OrderByDescending(giveaway => giveaway.EndsAt)
            .ToList();
    }

    /// <summary>Everything the guild has, running first then most recently ended.</summary>
    public IReadOnlyList<Giveaway> GetAll(ulong guildId)
    {
        return _giveaways.Values
            .Where(giveaway => giveaway.GuildId == guildId)
            .OrderBy(giveaway => giveaway.IsEnded)
            .ThenBy(giveaway => giveaway.EndsAt)
            .ToList();
    }

    public async Task<GiveawayStartOutcome> StartAsync(
        SocketGuildUser host,
        SocketTextChannel channel,
        TimeSpan duration,
        int winnerCount,
        string prize)
    {
        if (GetRunning(channel.Guild.Id).Count >= MaxActivePerGuild)
        {
            return new GiveawayStartOutcome(
                GiveawayStartResult.LimitReached,
                null,
                IsPersistent);
        }

        var now = DateTimeOffset.UtcNow;

        var draft = new Giveaway
        {
            GuildId = channel.Guild.Id,
            ChannelId = channel.Id,
            HostId = host.Id,
            Prize = prize,
            WinnerCount = winnerCount,
            CreatedAt = now.ToUnixTimeSeconds(),
            EndsAt = now.Add(duration).ToUnixTimeSeconds()
        };

        IUserMessage card;

        try
        {
            card = await channel.SendMessageAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildCard(draft, channel.Guild));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Giveaway Post Error] {exception.Message}");

            return new GiveawayStartOutcome(
                GiveawayStartResult.PostFailed,
                null,
                IsPersistent);
        }

        // The card's message id is both the entity key and the enter button's
        // payload, and it does not exist until the message is sent — so a draft goes
        // out first and is immediately re-rendered with the real id.
        var giveaway = new Giveaway
        {
            GuildId = draft.GuildId,
            ChannelId = draft.ChannelId,
            MessageId = card.Id,
            HostId = draft.HostId,
            Prize = draft.Prize,
            WinnerCount = draft.WinnerCount,
            CreatedAt = draft.CreatedAt,
            EndsAt = draft.EndsAt
        };

        await CommitAsync(giveaway);

        try
        {
            await card.ModifyAsync(
                properties => ApplyCard(properties, giveaway, channel.Guild));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Giveaway Refresh Error] {exception.Message}");
        }

        return new GiveawayStartOutcome(
            GiveawayStartResult.Started,
            giveaway,
            IsPersistent);
    }

    /// <summary>
    /// Enter button. Pressing it again leaves, so one handler covers both directions
    /// and the stored entry set stays exactly what <c>gentries</c> reports.
    /// </summary>
    public async Task<GiveawayEntryOutcome> ToggleEntryAsync(
        ulong messageId,
        SocketGuildUser member)
    {
        if (!_giveaways.TryGetValue(messageId, out var existing))
            return new GiveawayEntryOutcome(GiveawayEntryResult.NotFound, false, 0);

        if (existing.IsEnded)
        {
            return new GiveawayEntryOutcome(
                GiveawayEntryResult.Ended,
                false,
                existing.EntryIds.Count);
        }

        var giveaway = existing.Clone();
        var joined = giveaway.EntryIds.Add(member.Id);

        if (!joined)
            giveaway.EntryIds.Remove(member.Id);

        await CommitAsync(giveaway);

        // Editing the card on every click would hit the rate limit, so a click only
        // marks it dirty and the ticker refreshes in batches. The member still gets
        // instant feedback from the ephemeral reply.
        _dirty[messageId] = true;

        return new GiveawayEntryOutcome(
            GiveawayEntryResult.Done,
            joined,
            giveaway.EntryIds.Count);
    }

    public async Task<GiveawayDrawOutcome> EndAsync(ulong messageId)
    {
        if (!_giveaways.TryGetValue(messageId, out var existing))
            return new GiveawayDrawOutcome(GiveawayDrawResult.NotFound, null);

        if (existing.IsEnded)
            return new GiveawayDrawOutcome(GiveawayDrawResult.AlreadyEnded, existing);

        return await DrawAsync(messageId, isReroll: false);
    }

    public async Task<GiveawayDrawOutcome> RerollAsync(ulong messageId)
    {
        if (!_giveaways.TryGetValue(messageId, out var existing))
            return new GiveawayDrawOutcome(GiveawayDrawResult.NotFound, null);

        if (existing.IsRunning)
            return new GiveawayDrawOutcome(GiveawayDrawResult.StillRunning, existing);

        return await DrawAsync(messageId, isReroll: true);
    }

    /// <summary>
    /// Forces the next draw's first winner, or clears the force when
    /// <paramref name="userId"/> is null. Only ever reached through the owner-gated
    /// command.
    /// </summary>
    public async Task<bool> SetRiggedWinnerAsync(ulong messageId, ulong? userId)
    {
        if (!_giveaways.TryGetValue(messageId, out var existing))
            return false;

        var giveaway = existing.Clone();
        giveaway.RiggedWinnerId = userId;

        await CommitAsync(giveaway);
        return true;
    }

    private async Task<GiveawayDrawOutcome> DrawAsync(ulong messageId, bool isReroll)
    {
        if (!_giveaways.TryGetValue(messageId, out var existing))
            return new GiveawayDrawOutcome(GiveawayDrawResult.NotFound, null);

        if (_client.GetGuild(existing.GuildId) is not { } guild)
            return new GiveawayDrawOutcome(GiveawayDrawResult.GuildUnavailable, existing);

        var giveaway = existing.Clone();
        var winners = DrawWinners(giveaway, guild);

        // The force is good for one draw only. Consuming it here and pushing every
        // winner into the past-winner set is what makes a later reroll genuinely
        // random — the forced member can never be drawn again.
        giveaway.RiggedWinnerId = null;
        giveaway.WinnerIds = winners;
        giveaway.IsEnded = true;

        // Ending early means the card's "Ended" stamp should read now; one that ran
        // its full course already carries the correct scheduled stamp.
        if (!isReroll && !giveaway.HasExpired)
            giveaway.EndsAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        foreach (var winner in winners)
            giveaway.PastWinnerIds.Add(winner);

        await CommitAsync(giveaway);
        await AnnounceAsync(giveaway, guild, isReroll);
        await RefreshCardAsync(giveaway);

        _dirty.TryRemove(messageId, out _);

        return new GiveawayDrawOutcome(
            winners.Count == 0 ? GiveawayDrawResult.NoWinners : GiveawayDrawResult.Done,
            giveaway);
    }

    private static List<ulong> DrawWinners(Giveaway giveaway, SocketGuild guild)
    {
        var winners = new List<ulong>();

        // The forced winner takes the first slot whether or not they pressed enter.
        // If they have left the server it is skipped silently and the draw stays
        // completely random.
        if (giveaway.RiggedWinnerId is { } riggedId && guild.GetUser(riggedId) is not null)
            winners.Add(riggedId);

        var pool = giveaway.EntryIds
            .Where(entryId => !giveaway.PastWinnerIds.Contains(entryId))
            .Where(entryId => !winners.Contains(entryId))
            .Where(entryId => guild.GetUser(entryId) is not null)
            .ToList();

        while (winners.Count < giveaway.WinnerCount && pool.Count > 0)
        {
            var index = Random.Shared.Next(pool.Count);

            winners.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return winners;
    }

    private async Task AnnounceAsync(Giveaway giveaway, SocketGuild guild, bool isReroll)
    {
        if (guild.GetTextChannel(giveaway.ChannelId) is not { } channel)
            return;

        try
        {
            await channel.SendMessageAsync(
                allowedMentions: new AllowedMentions
                {
                    // Nothing outside the whitelist can ping, so even a prize
                    // containing @everyone or a role mention only notifies winners.
                    AllowedTypes = AllowedMentionTypes.None,
                    UserIds = giveaway.WinnerIds.ToList()
                },
                components: _builder.BuildAnnouncement(giveaway, guild, isReroll));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Giveaway Announce Error] {exception.Message}");
        }
    }

    /// <summary>
    /// Best-effort card edit. Correctness never depends on it — the enter button
    /// rechecks live state on click — so a stale card is only cosmetic.
    /// </summary>
    private async Task RefreshCardAsync(Giveaway giveaway)
    {
        if (_client.GetGuild(giveaway.GuildId) is not { } guild)
            return;

        if (guild.GetTextChannel(giveaway.ChannelId) is not { } channel)
            return;

        try
        {
            if (await channel.GetMessageAsync(giveaway.MessageId) is not IUserMessage card)
                return;

            await card.ModifyAsync(properties => ApplyCard(properties, giveaway, guild));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Giveaway Refresh Error] {exception.Message}");
        }
    }

    private void ApplyCard(MessageProperties properties, Giveaway giveaway, SocketGuild guild)
    {
        // Editing a V2 card means resending the flag, otherwise Discord treats the
        // components as legacy ones and rejects them.
        properties.Flags = MessageFlags.ComponentsV2;
        properties.AllowedMentions = AllowedMentions.None;
        properties.Components = _builder.BuildCard(giveaway, guild);
    }

    private void StartTicker()
    {
        if (_tickerTask is { IsCompleted: false })
            return;

        _tickerTask = RunTickerAsync();
    }

    private async Task RunTickerAsync()
    {
        while (true)
        {
            await Task.Delay(TickInterval);

            try
            {
                // LoadAsync runs before login, so the guild cache is empty for the
                // first few ticks and there is nothing worth doing.
                if (_client.ConnectionState != ConnectionState.Connected)
                    continue;

                await ProcessTickAsync();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[Giveaway Tick Error] {exception}");
            }
        }
    }

    private async Task ProcessTickAsync()
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-RetentionDays);

        foreach (var giveaway in _giveaways.Values.ToArray())
        {
            if (giveaway.IsRunning)
            {
                if (giveaway.HasExpired)
                    await DrawAsync(giveaway.MessageId, isReroll: false);

                continue;
            }

            if (giveaway.EndsAtUtc > cutoff)
                continue;

            if (_giveaways.TryRemove(giveaway.MessageId, out _))
                await DeleteDocumentAsync(giveaway.MessageId);

            _dirty.TryRemove(giveaway.MessageId, out _);
        }

        foreach (var messageId in _dirty.Keys.ToArray())
        {
            if (!_dirty.TryRemove(messageId, out _))
                continue;

            if (_giveaways.TryGetValue(messageId, out var giveaway))
                await RefreshCardAsync(giveaway);
        }
    }

    private async Task<bool> CommitAsync(Giveaway giveaway)
    {
        _giveaways[giveaway.MessageId] = giveaway;

        if (_collection is null)
            return false;

        try
        {
            var document = ToDocument(giveaway);

            var filter = Builders<GiveawayDocument>.Filter.Eq(
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
            Console.WriteLine($"[Giveaway Save Error] {exception}");
            return false;
        }
    }

    private async Task DeleteDocumentAsync(ulong messageId)
    {
        if (_collection is null)
            return;

        try
        {
            var filter = Builders<GiveawayDocument>.Filter.Eq(
                existing => existing.Id,
                messageId.ToString());

            await _collection.DeleteOneAsync(filter);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Giveaway Delete Error] {exception}");
        }
    }

    private static GiveawayDocument ToDocument(Giveaway giveaway)
    {
        return new GiveawayDocument
        {
            Id = giveaway.MessageId.ToString(),
            GuildId = giveaway.GuildId.ToString(),
            ChannelId = giveaway.ChannelId.ToString(),
            HostId = giveaway.HostId.ToString(),
            Prize = giveaway.Prize,
            WinnerCount = giveaway.WinnerCount,
            CreatedAt = giveaway.CreatedAt,
            EndsAt = giveaway.EndsAt,
            Ended = giveaway.IsEnded,
            Entries = giveaway.EntryIds.Select(entryId => entryId.ToString()).ToList(),
            Rigged = giveaway.RiggedWinnerId?.ToString(),
            Winners = giveaway.WinnerIds.Select(winnerId => winnerId.ToString()).ToList(),
            PastWinners = giveaway.PastWinnerIds
                .Select(winnerId => winnerId.ToString())
                .ToList()
        };
    }

    private static Giveaway? FromDocument(GiveawayDocument document)
    {
        if (!ulong.TryParse(document.Id, out var messageId) ||
            !ulong.TryParse(document.GuildId, out var guildId) ||
            !ulong.TryParse(document.ChannelId, out var channelId))
        {
            return null;
        }

        _ = ulong.TryParse(document.HostId, out var hostId);

        var giveaway = new Giveaway
        {
            GuildId = guildId,
            ChannelId = channelId,
            MessageId = messageId,
            HostId = hostId,
            Prize = document.Prize,
            WinnerCount = Math.Clamp(document.WinnerCount, 1, MaxWinners),
            CreatedAt = document.CreatedAt,
            EndsAt = document.EndsAt,
            IsEnded = document.Ended
        };

        foreach (var entry in ParseIds(document.Entries))
            giveaway.EntryIds.Add(entry);

        foreach (var winner in ParseIds(document.PastWinners))
            giveaway.PastWinnerIds.Add(winner);

        giveaway.WinnerIds = ParseIds(document.Winners).ToList();

        if (ulong.TryParse(document.Rigged, out var rigged))
            giveaway.RiggedWinnerId = rigged;

        return giveaway;
    }

    private static IEnumerable<ulong> ParseIds(IEnumerable<string>? values)
    {
        if (values is null)
            yield break;

        foreach (var value in values)
        {
            if (ulong.TryParse(value, out var parsed))
                yield return parsed;
        }
    }
}

public enum GiveawayStartResult
{
    Started,
    LimitReached,
    PostFailed
}

public enum GiveawayEntryResult
{
    Done,
    NotFound,
    Ended
}

public enum GiveawayDrawResult
{
    Done,
    NoWinners,
    NotFound,
    AlreadyEnded,
    StillRunning,
    GuildUnavailable
}

public readonly record struct GiveawayStartOutcome(
    GiveawayStartResult Result,
    Giveaway? Giveaway,
    bool Persisted);

public readonly record struct GiveawayEntryOutcome(
    GiveawayEntryResult Result,
    bool Joined,
    int EntryCount);

public readonly record struct GiveawayDrawOutcome(
    GiveawayDrawResult Result,
    Giveaway? Giveaway);
