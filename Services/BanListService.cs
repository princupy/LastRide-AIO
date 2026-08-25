using System.Collections.Concurrent;
using LastRide.Models;

namespace LastRide.Services;

public sealed class BanListService
{
    public const int PageSize = 5;

    private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, BanListSession> _sessions = new();
    private readonly object _sync = new();

    public BanListSession Create(
        ulong guildId,
        ulong requesterId,
        IReadOnlyList<BannedUser> bans)
    {
        CleanupExpired();

        var id = Guid.NewGuid().ToString("N")[..12];
        var session = new BanListSession(
            id,
            guildId,
            requesterId,
            new List<BannedUser>(bans),
            DateTimeOffset.UtcNow);

        _sessions[id] = session;
        return session;
    }

    public bool TryGet(string id, out BanListSession session)
    {
        session = null!;

        if (!_sessions.TryGetValue(id, out var found))
            return false;

        if (IsExpired(found))
        {
            _sessions.TryRemove(id, out _);
            return false;
        }

        session = found;
        return true;
    }

    public void RemoveUser(string id, ulong userId)
    {
        if (!_sessions.TryGetValue(id, out var session))
            return;

        lock (_sync)
        {
            session.Bans.RemoveAll(ban => ban.UserId == userId);
        }
    }

    public void ClearUsers(string id)
    {
        if (!_sessions.TryGetValue(id, out var session))
            return;

        lock (_sync)
        {
            session.Bans.Clear();
        }
    }

    private void CleanupExpired()
    {
        foreach (var session in _sessions.Values)
        {
            if (IsExpired(session))
                _sessions.TryRemove(session.Id, out _);
        }
    }

    private static bool IsExpired(BanListSession session)
    {
        return DateTimeOffset.UtcNow - session.CreatedAt > Expiry;
    }
}
