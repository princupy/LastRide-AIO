namespace LastRide.Models;

/// <summary>
/// Runtime ticket configuration for a single guild: where ticket channels are
/// created, which roles staff them, and the templates shown on the panel and
/// inside a new ticket. Held in the service cache and read by the panel button.
/// Mutations are performed copy-on-write so readers always see a consistent
/// snapshot without locking.
/// </summary>
public sealed class TicketConfig
{
    public ulong GuildId { get; init; }

    public bool Enabled { get; set; }

    /// <summary>Category new ticket channels are created under.</summary>
    public ulong? CategoryId { get; set; }

    /// <summary>Channel transcripts and close summaries are posted to.</summary>
    public ulong? LogChannelId { get; set; }

    public HashSet<ulong> SupportRoleIds { get; init; } = new();

    /// <summary>Message posted inside a freshly opened ticket.</summary>
    public string? OpenMessage { get; set; }

    /// <summary>Message shown on the panel that carries the create button.</summary>
    public string? PanelMessage { get; set; }

    /// <summary>How many tickets one member may have open at the same time.</summary>
    public int Limit { get; set; } = 1;

    /// <summary>Last handed-out ticket number; incremented per ticket.</summary>
    public int Counter { get; set; }

    /// <summary>Tickets can only be opened once enabled and given a category.</summary>
    public bool IsReady => Enabled && CategoryId is not null;

    public bool HasSupportRole(IEnumerable<ulong> roleIds)
    {
        return SupportRoleIds.Count > 0 && roleIds.Any(SupportRoleIds.Contains);
    }

    public TicketConfig Clone()
    {
        return new TicketConfig
        {
            GuildId = GuildId,
            Enabled = Enabled,
            CategoryId = CategoryId,
            LogChannelId = LogChannelId,
            SupportRoleIds = new HashSet<ulong>(SupportRoleIds),
            OpenMessage = OpenMessage,
            PanelMessage = PanelMessage,
            Limit = Limit,
            Counter = Counter
        };
    }
}
