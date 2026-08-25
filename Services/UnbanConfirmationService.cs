using System.Collections.Concurrent;
using LastRide.Models;

namespace LastRide.Services;

public sealed class UnbanConfirmationService
{
    private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<string, PendingUnbanRequest> _requests = new();

    public PendingUnbanRequest Create(
        ulong guildId,
        ulong requesterId,
        ulong targetId,
        string targetName,
        string? targetAvatarUrl,
        string reason)
    {
        CleanupExpired();

        var id = Guid.NewGuid().ToString("N")[..12];
        var request = new PendingUnbanRequest(
            id,
            guildId,
            requesterId,
            targetId,
            targetName,
            targetAvatarUrl,
            reason,
            DateTimeOffset.UtcNow);

        _requests[id] = request;
        return request;
    }

    public bool TryGet(string id, out PendingUnbanRequest request)
    {
        request = null!;

        if (!_requests.TryGetValue(id, out var pending))
            return false;

        if (IsExpired(pending))
        {
            _requests.TryRemove(id, out _);
            return false;
        }

        request = pending;
        return true;
    }

    public bool TryRemove(string id, out PendingUnbanRequest request)
    {
        return _requests.TryRemove(id, out request!);
    }

    private void CleanupExpired()
    {
        foreach (var request in _requests.Values)
        {
            if (IsExpired(request))
                _requests.TryRemove(request.Id, out _);
        }
    }

    private static bool IsExpired(PendingUnbanRequest request)
    {
        return DateTimeOffset.UtcNow - request.CreatedAt > Expiry;
    }
}
