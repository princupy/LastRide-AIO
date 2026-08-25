using System.Collections.Concurrent;
using LastRide.Models;

namespace LastRide.Services;

public sealed class AfkService
{
    private readonly ConcurrentDictionary<ulong, AfkStatus> _statuses = new();

    public AfkStatus SetAfk(
        ulong userId,
        string displayName,
        string? reason)
    {
        var status = new AfkStatus(
            userId,
            displayName,
            NormalizeReason(reason),
            DateTimeOffset.UtcNow);

        _statuses[userId] = status;

        return status;
    }

    public bool TryGetAfk(
        ulong userId,
        out AfkStatus status)
    {
        return _statuses.TryGetValue(userId, out status!);
    }

    public bool TryClearAfk(
        ulong userId,
        out AfkStatus status)
    {
        return _statuses.TryRemove(userId, out status!);
    }

    private static string NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return "No reason provided.";

        reason = reason.Trim();

        return reason.Length <= 800
            ? reason
            : reason[..800] + "...";
    }
}
