namespace LastRide.Builders;

/// <summary>
/// How long a no-prefix grant lasts. A fixed list rather than free-text parsing,
/// because the only way to pick one is the dropdown that follows <c>nop add</c>.
/// </summary>
public enum NoPrefixDuration
{
    OneDay,
    FifteenDays,
    ThirtyDays,
    ThreeMonths,
    OneYear,
    Permanent
}

/// <summary>
/// Custom-ID plumbing for the no-prefix components. Two shapes share one prefix:
/// the duration dropdown carries the member the grant is for, and the list
/// navigation carries its page. Both keep the requester so nobody else can finish
/// or steer a flow the owner started.
/// </summary>
public static class NoPrefixComponentIds
{
    private const string Prefix = "noprefix";

    /// <summary>
    /// Duration dropdown shown after <c>nop add</c>. The target travels inside the
    /// ID so no server-side pending state is kept between the command and the pick.
    /// </summary>
    public static string CreateDurationMenu(ulong targetId, ulong requesterId)
    {
        return $"{Prefix}:duration:{targetId}:{requesterId}";
    }

    public static bool TryParseDurationMenu(
        string? customId,
        out ulong targetId,
        out ulong requesterId)
    {
        targetId = 0;
        requesterId = 0;

        if (string.IsNullOrWhiteSpace(customId))
            return false;

        var parts = customId.Split(':');

        if (parts.Length != 4 ||
            parts[0] != Prefix ||
            parts[1] != "duration")
        {
            return false;
        }

        return ulong.TryParse(parts[2], out targetId) &&
            ulong.TryParse(parts[3], out requesterId);
    }

    /// <summary>
    /// List page navigation. Everything the handler needs travels inside the ID, so
    /// no session is kept and a page flip always re-reads the live grant list.
    /// </summary>
    public static string CreateListNav(int page, ulong requesterId)
    {
        return $"{Prefix}:list:{page}:{requesterId}";
    }

    public static bool TryParseListNav(
        string? customId,
        out int page,
        out ulong requesterId)
    {
        page = 0;
        requesterId = 0;

        if (string.IsNullOrWhiteSpace(customId))
            return false;

        var parts = customId.Split(':');

        if (parts.Length != 4 ||
            parts[0] != Prefix ||
            parts[1] != "list")
        {
            return false;
        }

        return int.TryParse(parts[2], out page) &&
            ulong.TryParse(parts[3], out requesterId);
    }

    public static bool TryParseDuration(
        string? value,
        out NoPrefixDuration duration)
    {
        duration = NoPrefixDuration.OneDay;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        switch (value)
        {
            case "1d":
                duration = NoPrefixDuration.OneDay;
                return true;

            case "15d":
                duration = NoPrefixDuration.FifteenDays;
                return true;

            case "30d":
                duration = NoPrefixDuration.ThirtyDays;
                return true;

            case "3m":
                duration = NoPrefixDuration.ThreeMonths;
                return true;

            case "1y":
                duration = NoPrefixDuration.OneYear;
                return true;

            case "permanent":
                duration = NoPrefixDuration.Permanent;
                return true;

            default:
                return false;
        }
    }

    public static string ToValue(NoPrefixDuration duration)
    {
        return duration switch
        {
            NoPrefixDuration.OneDay => "1d",
            NoPrefixDuration.FifteenDays => "15d",
            NoPrefixDuration.ThirtyDays => "30d",
            NoPrefixDuration.ThreeMonths => "3m",
            NoPrefixDuration.OneYear => "1y",
            _ => "permanent"
        };
    }

    public static string ToLabel(NoPrefixDuration duration)
    {
        return duration switch
        {
            NoPrefixDuration.OneDay => "1 day",
            NoPrefixDuration.FifteenDays => "15 days",
            NoPrefixDuration.ThirtyDays => "30 days",
            NoPrefixDuration.ThreeMonths => "3 months",
            NoPrefixDuration.OneYear => "1 year",
            _ => "Permanent"
        };
    }

    /// <summary>
    /// Length of a grant, or <c>null</c> for one that never expires. Months and
    /// years are fixed day counts so the stamp is decided entirely by the picked
    /// option and never drifts with the calendar.
    /// </summary>
    public static TimeSpan? ToTimeSpan(NoPrefixDuration duration)
    {
        return duration switch
        {
            NoPrefixDuration.OneDay => TimeSpan.FromDays(1),
            NoPrefixDuration.FifteenDays => TimeSpan.FromDays(15),
            NoPrefixDuration.ThirtyDays => TimeSpan.FromDays(30),
            NoPrefixDuration.ThreeMonths => TimeSpan.FromDays(90),
            NoPrefixDuration.OneYear => TimeSpan.FromDays(365),
            _ => null
        };
    }
}
