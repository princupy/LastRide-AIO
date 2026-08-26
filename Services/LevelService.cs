using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Models;
using MongoDB.Driver;

namespace LastRide.Services;

public sealed class LevelService
{
    /// <summary>Voice XP handed out for every full minute spent in a channel.</summary>
    public const int VoiceXpPerMinute = LevelDefaults.VoiceXpPerMinute;

    /// <summary>Safety clamp so a stale session can never pay out days of XP.</summary>
    private const double MaxSessionMinutes = 1440;

    private const string DatabaseName = "lastride";
    private const string CollectionName = "level_users";

    private const string DefaultLevelUpMessage =
        "{user} just leveled up to Level {level}!";

    private readonly LevelConfigService _configService;
    private readonly LevelComponentBuilder _builder;
    private readonly IMongoCollection<LevelUserDocument>? _collection;

    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), LevelUser> _users = new();
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), DateTimeOffset> _textCooldowns = new();
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), DateTimeOffset> _voiceSessions = new();

    public LevelService(
        LevelConfigService configService,
        MongoDbService mongo,
        LevelComponentBuilder builder)
    {
        _configService = configService;
        _builder = builder;
        _collection = mongo.GetCollection<LevelUserDocument>(
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
                .Find(Builders<LevelUserDocument>.Filter.Empty)
                .ToListAsync();

            foreach (var document in documents)
            {
                var user = FromDocument(document);

                if (user is not null)
                    _users[(user.GuildId, user.UserId)] = user;
            }

            Console.WriteLine(
                $"[Leveling] Loaded {_users.Count} member XP record(s) from database.");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Leveling Load Error] {exception}");
        }
    }

    public async Task HandleTextMessageAsync(SocketMessage message)
    {
        try
        {
            if (message is not SocketUserMessage userMessage)
                return;

            if (userMessage.Author is not SocketGuildUser author || author.IsBot)
                return;

            var guild = author.Guild;
            var config = _configService.GetConfig(guild.Id);

            if (!config.Enabled)
                return;

            if (config.IsChannelBlacklisted(userMessage.Channel.Id))
                return;

            if (config.HasBlacklistedRole(author.Roles.Select(role => role.Id)))
                return;

            var key = (guild.Id, author.Id);
            var now = DateTimeOffset.UtcNow;

            if (_textCooldowns.TryGetValue(key, out var lastAt) &&
                (now - lastAt).TotalSeconds < config.XpCooldownSeconds)
            {
                return;
            }

            _textCooldowns[key] = now;

            var gained = Random.Shared.Next(
                config.MinXpPerMessage,
                config.MaxXpPerMessage + 1);

            if (gained <= 0)
                return;

            var user = GetOrCreateUser(guild.Id, author.Id);
            var previousLevel = LevelMath.LevelForXp(user.Xp);
            user.Xp += gained;
            var currentLevel = LevelMath.LevelForXp(user.Xp);

            await CommitUserAsync(user);

            if (currentLevel <= previousLevel)
                return;

            var awardedRoleId = await ReconcileLevelRolesAsync(author, currentLevel, config);

            if (config.LevelUpAnnouncementsEnabled)
            {
                await AnnounceLevelUpAsync(
                    author,
                    userMessage.Channel,
                    currentLevel,
                    config,
                    awardedRoleId);
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Leveling Text Error] {exception}");
        }
    }

    public async Task HandleVoiceStateUpdatedAsync(
        SocketUser socketUser,
        SocketVoiceState before,
        SocketVoiceState after)
    {
        try
        {
            if (socketUser is not SocketGuildUser user || user.IsBot)
                return;

            var from = before.VoiceChannel;
            var to = after.VoiceChannel;

            // Ignore state-only changes (mute/deafen/stream) where the channel is
            // unchanged — only join/leave/move start or settle a session.
            if (from?.Id == to?.Id)
                return;

            var guild = user.Guild;
            var config = _configService.GetConfig(guild.Id);

            if (!config.Enabled)
                return;

            var key = (guild.Id, user.Id);

            if (from is not null && _voiceSessions.TryRemove(key, out var startedAt))
            {
                var minutes = Math.Clamp(
                    (DateTimeOffset.UtcNow - startedAt).TotalMinutes,
                    0,
                    MaxSessionMinutes);

                var gained = (long)Math.Floor(minutes * VoiceXpPerMinute);

                if (gained > 0)
                {
                    var record = GetOrCreateUser(guild.Id, user.Id);
                    record.VoiceXp += gained;
                    await CommitUserAsync(record);
                }
            }

            // AFK parking should not pay out, so no session is opened there.
            if (to is not null && to.Id != guild.AFKChannel?.Id)
                _voiceSessions[key] = DateTimeOffset.UtcNow;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Leveling Voice Error] {exception}");
        }
    }

    public async Task<LevelXpUpdate> AddTextXpAsync(SocketGuildUser member, long amount)
    {
        var config = _configService.GetConfig(member.Guild.Id);
        var user = GetOrCreateUser(member.Guild.Id, member.Id);

        var previousLevel = LevelMath.LevelForXp(user.Xp);
        user.Xp = ClampXp(user.Xp + amount);
        var currentLevel = LevelMath.LevelForXp(user.Xp);

        var persisted = await CommitUserAsync(user);
        await ReconcileLevelRolesAsync(member, currentLevel, config);

        return new LevelXpUpdate(previousLevel, currentLevel, user.Xp, persisted);
    }

    public Task<LevelXpUpdate> RemoveTextXpAsync(SocketGuildUser member, long amount)
    {
        return AddTextXpAsync(member, -amount);
    }

    public async Task<LevelXpUpdate> SetTextLevelAsync(SocketGuildUser member, int level)
    {
        var config = _configService.GetConfig(member.Guild.Id);
        var user = GetOrCreateUser(member.Guild.Id, member.Id);

        var previousLevel = LevelMath.LevelForXp(user.Xp);
        user.Xp = LevelMath.TotalXpForLevel(level);
        var currentLevel = LevelMath.LevelForXp(user.Xp);

        var persisted = await CommitUserAsync(user);
        await ReconcileLevelRolesAsync(member, currentLevel, config);

        return new LevelXpUpdate(previousLevel, currentLevel, user.Xp, persisted);
    }

    public async Task<bool> ResetTextUserAsync(ulong guildId, ulong userId)
    {
        if (!_users.TryGetValue((guildId, userId), out var user))
            return IsPersistent;

        user.Xp = 0;
        return await CommitUserAsync(user);
    }

    public async Task<bool> ResetVoiceUserAsync(ulong guildId, ulong userId)
    {
        RestampVoiceSession(guildId, userId);

        if (!_users.TryGetValue((guildId, userId), out var user))
            return IsPersistent;

        user.VoiceXp = 0;
        return await CommitUserAsync(user);
    }

    public Task<bool> ResetTextAllAsync(ulong guildId)
    {
        return ResetAllAsync(guildId, voiceTrack: false);
    }

    public Task<bool> ResetVoiceAllAsync(ulong guildId)
    {
        return ResetAllAsync(guildId, voiceTrack: true);
    }

    public LevelUser GetUser(ulong guildId, ulong userId)
    {
        return _users.TryGetValue((guildId, userId), out var user)
            ? user
            : new LevelUser { GuildId = guildId, UserId = userId };
    }

    public IReadOnlyList<LevelUser> GetTextLeaderboard(ulong guildId)
    {
        return _users.Values
            .Where(user => user.GuildId == guildId && user.Xp > 0)
            .OrderByDescending(user => user.Xp)
            .ThenBy(user => user.UserId)
            .ToArray();
    }

    public IReadOnlyList<LevelUser> GetVoiceLeaderboard(ulong guildId)
    {
        return _users.Values
            .Where(user => user.GuildId == guildId && user.VoiceXp > 0)
            .OrderByDescending(user => user.VoiceXp)
            .ThenBy(user => user.UserId)
            .ToArray();
    }

    public LevelRank GetTextRank(ulong guildId, ulong userId)
    {
        return Rank(GetTextLeaderboard(guildId), userId);
    }

    public LevelRank GetVoiceRank(ulong guildId, ulong userId)
    {
        return Rank(GetVoiceLeaderboard(guildId), userId);
    }

    /// <summary>Re-applies every level role the member has earned at their text level.</summary>
    public Task<ulong?> SyncLevelRolesAsync(SocketGuildUser member)
    {
        var config = _configService.GetConfig(member.Guild.Id);
        var user = GetUser(member.Guild.Id, member.Id);
        return ReconcileLevelRolesAsync(member, LevelMath.LevelForXp(user.Xp), config);
    }

    private static LevelRank Rank(IReadOnlyList<LevelUser> ranked, ulong userId)
    {
        for (var index = 0; index < ranked.Count; index++)
        {
            if (ranked[index].UserId == userId)
                return new LevelRank(index + 1, ranked.Count);
        }

        return new LevelRank(0, ranked.Count);
    }

    private async Task<bool> ResetAllAsync(ulong guildId, bool voiceTrack)
    {
        foreach (var pair in _users)
        {
            if (pair.Key.GuildId != guildId)
                continue;

            if (voiceTrack)
                pair.Value.VoiceXp = 0;
            else
                pair.Value.Xp = 0;

            if (pair.Value.Xp <= 0 && pair.Value.VoiceXp <= 0)
                _users.TryRemove(pair.Key, out _);
        }

        if (voiceTrack)
        {
            foreach (var key in _voiceSessions.Keys)
            {
                if (key.GuildId == guildId)
                    _voiceSessions[key] = DateTimeOffset.UtcNow;
            }
        }

        if (_collection is null)
            return false;

        try
        {
            var filter = Builders<LevelUserDocument>.Filter.Eq(
                document => document.GuildId,
                guildId.ToString());

            var update = voiceTrack
                ? Builders<LevelUserDocument>.Update.Set(document => document.VoiceXp, 0)
                : Builders<LevelUserDocument>.Update.Set(document => document.Xp, 0);

            await _collection.UpdateManyAsync(filter, update);

            // Records with nothing left on either track are dropped entirely.
            await _collection.DeleteManyAsync(Builders<LevelUserDocument>.Filter.And(
                filter,
                Builders<LevelUserDocument>.Filter.Eq(document => document.Xp, 0),
                Builders<LevelUserDocument>.Filter.Eq(document => document.VoiceXp, 0)));

            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Leveling Save Error] {exception}");
            return false;
        }
    }

    private void RestampVoiceSession(ulong guildId, ulong userId)
    {
        var key = (guildId, userId);

        if (_voiceSessions.ContainsKey(key))
            _voiceSessions[key] = DateTimeOffset.UtcNow;
    }

    private LevelUser GetOrCreateUser(ulong guildId, ulong userId)
    {
        return _users.GetOrAdd(
            (guildId, userId),
            _ => new LevelUser { GuildId = guildId, UserId = userId });
    }

    private static long ClampXp(long xp)
    {
        return xp < 0 ? 0 : xp;
    }

    private async Task<bool> CommitUserAsync(LevelUser user)
    {
        var key = (user.GuildId, user.UserId);

        if (user.Xp <= 0 && user.VoiceXp <= 0)
        {
            _users.TryRemove(key, out _);
            return await DeleteUserAsync(user.GuildId, user.UserId);
        }

        _users[key] = user;

        if (_collection is null)
            return false;

        try
        {
            var document = ToDocument(user);

            var filter = Builders<LevelUserDocument>.Filter.Eq(
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

    private async Task<bool> DeleteUserAsync(ulong guildId, ulong userId)
    {
        if (_collection is null)
            return false;

        try
        {
            var filter = Builders<LevelUserDocument>.Filter.Eq(
                document => document.Id,
                DocumentId(guildId, userId));

            await _collection.DeleteOneAsync(filter);
            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Leveling Save Error] {exception}");
            return false;
        }
    }

    /// <summary>
    /// Brings a member's reward roles in line with their text level. Returns the
    /// highest reward role they now hold, so the level-up card can mention it.
    /// </summary>
    private static async Task<ulong?> ReconcileLevelRolesAsync(
        SocketGuildUser member,
        int level,
        LevelConfig config)
    {
        if (config.LevelRoles.Count == 0)
            return null;

        var guild = member.Guild;
        var highestRoleId = config.HighestRoleUpTo(level);

        if (config.RoleMode == LevelRoleMode.Replace)
        {
            if (highestRoleId is not null)
            {
                await TryAddRoleAsync(
                    guild,
                    member,
                    highestRoleId.Value,
                    $"Leveling reward for level {level}");
            }

            foreach (var (_, roleId) in config.LevelRoles)
            {
                if (roleId == highestRoleId)
                    continue;

                await TryRemoveRoleAsync(
                    guild,
                    member,
                    roleId,
                    "Leveling reward replaced by a higher one");
            }

            return highestRoleId;
        }

        foreach (var (rewardLevel, roleId) in config.RolesUpTo(level))
        {
            await TryAddRoleAsync(
                guild,
                member,
                roleId,
                $"Leveling reward for level {rewardLevel}");
        }

        return highestRoleId;
    }

    private static bool CanAssignRole(SocketGuild guild, SocketRole? role)
    {
        if (role is null)
            return false;

        if (role.Id == guild.EveryoneRole.Id || role.IsManaged)
            return false;

        if (!guild.CurrentUser.GuildPermissions.ManageRoles &&
            !guild.CurrentUser.GuildPermissions.Administrator)
        {
            return false;
        }

        // The bot can only grant roles positioned below its own highest role.
        return role.Position < guild.CurrentUser.Hierarchy;
    }

    private static async Task TryAddRoleAsync(
        SocketGuild guild,
        SocketGuildUser member,
        ulong roleId,
        string reason)
    {
        var role = guild.GetRole(roleId);

        if (!CanAssignRole(guild, role))
            return;

        if (member.Roles.Any(existing => existing.Id == roleId))
            return;

        try
        {
            await member.AddRoleAsync(role, new RequestOptions { AuditLogReason = reason });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Leveling Role Add Error] {roleId}: {exception.Message}");
        }
    }

    private static async Task TryRemoveRoleAsync(
        SocketGuild guild,
        SocketGuildUser member,
        ulong roleId,
        string reason)
    {
        var role = guild.GetRole(roleId);

        if (role is null)
            return;

        if (member.Roles.All(existing => existing.Id != roleId))
            return;

        try
        {
            await member.RemoveRoleAsync(role, new RequestOptions { AuditLogReason = reason });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Leveling Role Remove Error] {roleId}: {exception.Message}");
        }
    }

    private async Task AnnounceLevelUpAsync(
        SocketGuildUser member,
        ISocketMessageChannel origin,
        int level,
        LevelConfig config,
        ulong? awardedRoleId)
    {
        var channel = origin;

        if (config.LevelUpChannelId is ulong channelId)
        {
            var configured = member.Guild.GetTextChannel(channelId);

            if (configured is not null)
                channel = configured;
        }

        var rendered = RenderLevelUpMessage(
            config.LevelUpMessage ?? DefaultLevelUpMessage,
            member,
            level);

        var components = _builder.BuildLevelUp(member, level, rendered, awardedRoleId);

        try
        {
            // `AllowedTypes` has to be set explicitly: with it left unset the
            // payload carries no `parse` field and the whitelist below is never
            // applied, so the member who levelled up never got pinged.
            await channel.SendMessageAsync(
                allowedMentions: new AllowedMentions
                {
                    AllowedTypes = AllowedMentionTypes.None,
                    UserIds = new List<ulong> { member.Id }
                },
                components: components);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Leveling Announce Error] {exception.Message}");
        }
    }

    private static string RenderLevelUpMessage(
        string template,
        SocketGuildUser member,
        int level)
    {
        return template
            .Replace("{user}", member.Mention)
            .Replace("{username}", member.DisplayName)
            .Replace("{level}", level.ToString())
            .Replace("{server}", member.Guild.Name);
    }

    private static string DocumentId(ulong guildId, ulong userId)
    {
        return $"{guildId}:{userId}";
    }

    private static LevelUserDocument ToDocument(LevelUser user)
    {
        return new LevelUserDocument
        {
            Id = DocumentId(user.GuildId, user.UserId),
            GuildId = user.GuildId.ToString(),
            UserId = user.UserId.ToString(),
            Xp = user.Xp,
            VoiceXp = user.VoiceXp
        };
    }

    private static LevelUser? FromDocument(LevelUserDocument document)
    {
        if (!ulong.TryParse(document.GuildId, out var guildId))
            return null;

        if (!ulong.TryParse(document.UserId, out var userId))
            return null;

        return new LevelUser
        {
            GuildId = guildId,
            UserId = userId,
            Xp = ClampXp(document.Xp),
            VoiceXp = ClampXp(document.VoiceXp)
        };
    }
}

public readonly record struct LevelRank(int Position, int Total);

public readonly record struct LevelXpUpdate(
    int PreviousLevel,
    int CurrentLevel,
    long TotalXp,
    bool Persisted);
