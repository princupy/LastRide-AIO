namespace LastRide.Models;

/// <summary>
/// Runtime record for one member's no-prefix access. The grant is global, so the
/// member id alone identifies it — there is no guild component. Mutated
/// copy-on-write like the guild configs.
/// </summary>
public sealed class NoPrefixEntry
{
    /// <summary>Member who may run commands without the prefix; also the cache key.</summary>
    public ulong UserId { get; init; }

    /// <summary>Owner who issued the grant.</summary>
    public ulong GrantedBy { get; init; }

    /// <summary>Unix seconds; stored as a plain number so BSON keeps it exact.</summary>
    public long GrantedAt { get; init; }

    /// <summary>
    /// Unix seconds; stored as a plain number so BSON keeps it exact. Zero means
    /// the grant never expires.
    /// </summary>
    public long ExpiresAt { get; init; }

    /// <summary>
    /// Label of the duration option that was picked, kept verbatim so the cards
    /// read back exactly what was chosen instead of re-deriving it from the stamp.
    /// </summary>
    public string DurationLabel { get; init; } = string.Empty;

    public bool IsPermanent => ExpiresAt == 0;

    public DateTimeOffset ExpiresAtUtc =>
        DateTimeOffset.FromUnixTimeSeconds(ExpiresAt);

    public bool HasExpired =>
        !IsPermanent && ExpiresAtUtc <= DateTimeOffset.UtcNow;

    public NoPrefixEntry Clone()
    {
        return new NoPrefixEntry
        {
            UserId = UserId,
            GrantedBy = GrantedBy,
            GrantedAt = GrantedAt,
            ExpiresAt = ExpiresAt,
            DurationLabel = DurationLabel
        };
    }
}
