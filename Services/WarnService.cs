using System.Collections.Concurrent;
using LastRide.Models;

namespace LastRide.Services;

public sealed class WarnService
{
    private const int MaxReasonLength = 800;

    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), List<Warning>>
        _warnings = new();

    private readonly object _sync = new();

    public (Warning Warning, int Count) AddWarning(
        ulong guildId,
        ulong userId,
        ulong moderatorId,
        string? reason)
    {
        var warning = new Warning(
            guildId,
            userId,
            moderatorId,
            NormalizeReason(reason),
            DateTimeOffset.UtcNow);

        var key = (guildId, userId);

        lock (_sync)
        {
            var list = _warnings.GetOrAdd(key, _ => new List<Warning>());
            list.Add(warning);

            return (warning, list.Count);
        }
    }

    public int GetWarningCount(ulong guildId, ulong userId)
    {
        lock (_sync)
        {
            return _warnings.TryGetValue((guildId, userId), out var list)
                ? list.Count
                : 0;
        }
    }

    private static string NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return "No reason provided.";

        reason = reason.Trim();

        return reason.Length <= MaxReasonLength
            ? reason
            : reason[..MaxReasonLength] + "...";
    }
}
