using Discord;
using Discord.WebSocket;
using LastRide.Models;
using LastRide.Services;

namespace LastRide.Builders;

/// <summary>
/// Renders the ticket panel, the cards posted inside a ticket, and the settings,
/// list, result, and notice cards the Ticket commands reply with.
/// </summary>
public sealed class TicketComponentBuilder
{
    private const int MaxListedTickets = 20;
    private const int MaxListedRoles = 20;

    private static readonly Color AccentColor = ComponentTheme.AccentColor;

    private const string EnabledEmoji = "<:Enabled:1541831759191212154>";
    private const string DisabledEmoji = "<:disabled:1541831823406014699>";

    /// <summary>
    /// The public panel carrying the create button. The button id is not scoped
    /// to a requester because any member is meant to be able to press it.
    /// </summary>
    public MessageComponent BuildPanel(SocketGuild guild, string message)
    {
        var header = new SectionBuilder()
            .WithAccessory(new ThumbnailBuilder(
                new UnfurledMediaItemProperties(GuildIconUrl(guild)),
                description: guild.Name))
            .AddComponents(
                new TextDisplayBuilder("## Support Tickets"),
                new TextDisplayBuilder(Quote(message)));

        var row = new ActionRowBuilder()
            .WithButton(ButtonBuilder.CreatePrimaryButton(
                "Create Ticket",
                TicketComponentIds.Create(TicketAction.New, guild.Id)));

        return BuildContainer(
            header,
            Divider(isDivider: false),
            row,
            FooterSeparator(),
            new TextDisplayBuilder(ComponentFooter.Text));
    }

    /// <summary>First card posted inside a freshly opened ticket.</summary>
    public MessageComponent BuildOpening(
        Ticket ticket,
        SocketGuildUser owner,
        string message,
        string? reason)
    {
        var lines = new List<string> { Quote(message) };

        if (!string.IsNullOrWhiteSpace(reason))
            lines.Add($"> **Reason:** {Inline(reason)}");

        var header = new SectionBuilder()
            .WithAccessory(new ThumbnailBuilder(
                new UnfurledMediaItemProperties(AvatarUrl(owner)),
                description: owner.DisplayName))
            .AddComponents(
                new TextDisplayBuilder($"## Ticket #{ticket.Number:D4}"),
                new TextDisplayBuilder(string.Join("\n", lines)));

        var row = new ActionRowBuilder()
            .WithButton(ButtonBuilder.CreateDangerButton(
                "Close Ticket",
                TicketComponentIds.Create(TicketAction.Close, ticket.ChannelId)))
            .WithButton(ButtonBuilder.CreateSecondaryButton(
                "Claim",
                TicketComponentIds.Create(TicketAction.Claim, ticket.ChannelId)));

        return BuildContainer(
            header,
            Divider(isDivider: false),
            row,
            FooterSeparator(),
            new TextDisplayBuilder(ComponentFooter.Text));
    }

    /// <summary>
    /// Card shown after a close: the channel is locked but kept, so staff still
    /// get reopen, delete, and transcript actions.
    /// </summary>
    public MessageComponent BuildClosed(
        Ticket ticket,
        SocketGuildUser actor,
        string? reason,
        bool transcriptSaved,
        bool logChannelSet)
    {
        var lines = new List<string>
        {
            $"> **Closed by:** {actor.Mention}",
            $"> **Opened by:** <@{ticket.OwnerId}>"
        };

        if (!string.IsNullOrWhiteSpace(reason))
            lines.Add($"> **Reason:** {Inline(reason)}");

        lines.Add(transcriptSaved
            ? "> **Transcript:** Saved to the ticket log channel."
            : logChannelSet
                ? "> **Transcript:** Could not be saved — check my access to the log channel."
                : "> **Transcript:** No log channel set, so nothing was saved.");

        var components = new List<IMessageComponentBuilder>
        {
            new TextDisplayBuilder(
                $"## Ticket #{ticket.Number:D4} Closed\n{string.Join("\n", lines)}"),
            Divider(isDivider: false),
            new ActionRowBuilder()
                .WithButton(ButtonBuilder.CreateSuccessButton(
                    "Reopen",
                    TicketComponentIds.Create(TicketAction.Reopen, ticket.ChannelId)))
                .WithButton(ButtonBuilder.CreateDangerButton(
                    "Delete",
                    TicketComponentIds.Create(TicketAction.Delete, ticket.ChannelId)))
                .WithButton(ButtonBuilder.CreateSecondaryButton(
                    "Transcript",
                    TicketComponentIds.Create(
                        TicketAction.Transcript,
                        ticket.ChannelId)))
        };

        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    /// <summary>Summary card that accompanies a transcript in the log channel.</summary>
    public MessageComponent BuildTranscriptLog(
        Ticket ticket,
        SocketGuild guild,
        SocketGuildUser actor,
        int messageCount,
        string? reason)
    {
        var lines = new List<string>
        {
            $"> **Channel:** `#{ChannelName(guild, ticket)}`",
            $"> **Opened by:** <@{ticket.OwnerId}> (`{ticket.OwnerId}`)",
            $"> **Closed by:** {actor.Mention} (`{actor.Id}`)",
            ticket.ClaimedBy is { } claimedBy
                ? $"> **Claimed by:** <@{claimedBy}>"
                : "> **Claimed by:** `Nobody`",
            $"> **Messages:** `{messageCount}`",
            $"> **Opened:** <t:{ticket.CreatedAt}:f>"
        };

        if (!string.IsNullOrWhiteSpace(reason))
            lines.Add($"> **Reason:** {Inline(reason)}");

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader(
                $"Ticket #{ticket.Number:D4} Transcript",
                string.Join("\n", lines),
                guild.IconUrl,
                guild.Name)
        };

        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    public MessageComponent BuildStatus(
        TicketConfig config,
        SocketGuild guild,
        string prefix,
        bool isPersistent,
        string? note)
    {
        var emoji = config.Enabled ? EnabledEmoji : DisabledEmoji;

        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(note))
            lines.Add($"> {note}");

        lines.Add($"> {emoji} **Status:** {(config.Enabled ? "Enabled" : "Disabled")}");
        lines.Add(config.CategoryId is { } categoryId
            ? $"> **Category:** {FormatCategory(guild, categoryId)}"
            : "> **Category:** `Not set`");
        lines.Add(config.LogChannelId is { } logChannelId
            ? $"> **Log Channel:** <#{logChannelId}>"
            : "> **Log Channel:** `Not set`");
        lines.Add($"> **Support Roles:** {FormatRoles(config.SupportRoleIds)}");
        lines.Add($"> **Limit Per Member:** `{config.Limit}`");
        lines.Add($"> **Open Message:** {FormatTemplate(config.OpenMessage)}");
        lines.Add($"> **Panel Message:** {FormatTemplate(config.PanelMessage)}");
        lines.Add($"> **Tickets Opened:** `{config.Counter}`");

        if (config.Enabled && config.CategoryId is null)
        {
            lines.Add(
                "> No tickets can be opened yet — a category still has to be set.");
        }

        var setup =
            "### Getting Started\n" +
            $"> `{prefix}ticketsetup` creates the category and log channel, then\n" +
            $"> `{prefix}ticketrole add @Support` and `{prefix}ticketpanel #channel`.";

        var hint =
            $"-# `{prefix}ticketcategory set <id>` • `{prefix}ticketlogs set #channel` • " +
            $"`{prefix}ticketmessage <text>` • `{prefix}ticketlimit <1-10>` • " +
            $"`{prefix}ticket on/off` • `{prefix}ticket reset`";

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader(
                "Ticket Settings",
                string.Join("\n", lines),
                guild.IconUrl,
                guild.Name),
            Divider(),
            new TextDisplayBuilder(setup),
            Divider(),
            new TextDisplayBuilder(hint)
        };

        AppendPersistenceNote(components, isPersistent);
        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    public MessageComponent BuildOpenList(
        IReadOnlyList<Ticket> tickets,
        SocketGuild guild,
        string prefix)
    {
        var lines = new List<string>();

        if (tickets.Count == 0)
        {
            lines.Add("> No tickets are open right now.");
        }
        else
        {
            foreach (var ticket in tickets.Take(MaxListedTickets))
            {
                var claim = ticket.ClaimedBy is { } claimedBy
                    ? $" • claimed by <@{claimedBy}>"
                    : string.Empty;

                lines.Add(
                    $"> `#{ticket.Number:D4}` <#{ticket.ChannelId}> — " +
                    $"<@{ticket.OwnerId}>{claim}");
            }

            if (tickets.Count > MaxListedTickets)
            {
                lines.Add(
                    $"> …and `{tickets.Count - MaxListedTickets}` more not shown.");
            }
        }

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader(
                $"Open Tickets ({tickets.Count})",
                string.Join("\n", lines),
                guild.IconUrl,
                guild.Name),
            Divider(),
            new TextDisplayBuilder(
                $"-# `{prefix}close <reason>` inside a ticket to close it • " +
                $"`{prefix}claim` to take it")
        };

        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    public MessageComponent BuildSupportRoleList(
        IReadOnlyCollection<ulong> roleIds,
        SocketGuild guild,
        string prefix,
        bool isPersistent)
    {
        var lines = new List<string>
        {
            roleIds.Count == 0
                ? "> No support roles configured — only admins can manage tickets."
                : $"> {FormatRoles(roleIds)}",
            $"> **Configured:** `{roleIds.Count}` / `{TicketConfigService.MaxSupportRoles}`"
        };

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader(
                "Ticket Support Roles",
                string.Join("\n", lines),
                guild.IconUrl,
                guild.Name),
            Divider(),
            new TextDisplayBuilder(
                $"-# `{prefix}ticketrole add @role` • `{prefix}ticketrole remove @role` • " +
                "these roles see and manage every ticket")
        };

        AppendPersistenceNote(components, isPersistent);
        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    /// <summary>Multi-line action confirmation used by the in-ticket commands.</summary>
    public MessageComponent BuildActionCard(string title, params string[] lines)
    {
        var body = string.Join(
            "\n",
            lines.Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => $"> {line}"));

        var components = new List<IMessageComponentBuilder>
        {
            new TextDisplayBuilder($"## {EscapeMarkdown(title)}\n{body}")
        };

        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    public MessageComponent BuildResult(string title, string message, bool isPersistent)
    {
        var components = new List<IMessageComponentBuilder>
        {
            new TextDisplayBuilder($"## {EscapeMarkdown(title)}\n> {message}")
        };

        AppendPersistenceNote(components, isPersistent);
        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    public MessageComponent BuildNotice(string title, string message)
    {
        return BuildContainer(
            new TextDisplayBuilder($"## {EscapeMarkdown(title)}\n> {message}"));
    }

    private static MessageComponent BuildContainer(
        params IMessageComponentBuilder[] components)
    {
        var container = new ContainerBuilder()
            .WithAccentColor(AccentColor)
            .AddComponents(components);

        return new ComponentBuilderV2().AddComponent(container).Build();
    }

    private static IMessageComponentBuilder BuildHeader(
        string title,
        string content,
        string? avatarUrl,
        string avatarDescription)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
            return new TextDisplayBuilder($"## {EscapeMarkdown(title)}\n{content}");

        return new SectionBuilder()
            .WithAccessory(new ThumbnailBuilder(
                new UnfurledMediaItemProperties(avatarUrl),
                description: avatarDescription))
            .AddComponents(
                new TextDisplayBuilder($"## {EscapeMarkdown(title)}"),
                new TextDisplayBuilder(content));
    }

    private static void AppendPersistenceNote(
        List<IMessageComponentBuilder> components,
        bool isPersistent)
    {
        if (isPersistent)
            return;

        components.Add(new TextDisplayBuilder(
            "-# Note: settings are active now but will reset when the bot restarts."));
    }

    private static void AppendFooter(List<IMessageComponentBuilder> components)
    {
        components.Add(FooterSeparator());
        components.Add(new TextDisplayBuilder(ComponentFooter.Text));
    }

    private static SeparatorBuilder Divider(bool isDivider = true)
    {
        return new SeparatorBuilder(
            isDivider: isDivider,
            spacing: SeparatorSpacingSize.Small);
    }

    private static SeparatorBuilder FooterSeparator()
    {
        return new SeparatorBuilder(isDivider: true, spacing: SeparatorSpacingSize.Small);
    }

    private static string Quote(string value)
    {
        // Quotes every line so a multi-line template keeps one continuous bar
        // instead of only marking the first line.
        return string.Join(
            "\n",
            value.Split('\n').Select(line => $"> {line.TrimEnd('\r')}"));
    }

    private static string FormatRoles(IReadOnlyCollection<ulong> roleIds)
    {
        if (roleIds.Count == 0)
            return "`None`";

        var mentions = roleIds
            .Take(MaxListedRoles)
            .Select(roleId => $"<@&{roleId}>");

        var text = string.Join(", ", mentions);

        return roleIds.Count > MaxListedRoles
            ? $"{text} …and `{roleIds.Count - MaxListedRoles}` more"
            : text;
    }

    private static string FormatCategory(SocketGuild guild, ulong categoryId)
    {
        var category = guild.CategoryChannels
            .FirstOrDefault(channel => channel.Id == categoryId);

        return category is null
            ? $"`Missing` (`{categoryId}`)"
            : $"`{EscapeInlineCode(category.Name)}`";
    }

    private static string ChannelName(SocketGuild guild, Ticket ticket)
    {
        var channel = guild.GetTextChannel(ticket.ChannelId);

        return channel is null
            ? $"ticket-{ticket.Number:D4}"
            : EscapeInlineCode(channel.Name);
    }

    private static string FormatTemplate(string? template)
    {
        return string.IsNullOrWhiteSpace(template)
            ? "`Default`"
            : $"`{EscapeInlineCode(Truncate(template, 120))}`";
    }

    private static string AvatarUrl(SocketGuildUser member)
    {
        return member.GetDisplayAvatarUrl(size: 256) ?? member.GetDefaultAvatarUrl();
    }

    private static string GuildIconUrl(SocketGuild guild)
    {
        return string.IsNullOrWhiteSpace(guild.IconUrl)
            ? guild.CurrentUser.GetDisplayAvatarUrl(size: 256)
              ?? guild.CurrentUser.GetDefaultAvatarUrl()
            : guild.IconUrl;
    }

    private static string Inline(string value)
    {
        return $"`{EscapeInlineCode(Truncate(value, 200))}`";
    }

    private static string Truncate(string value, int maxLength)
    {
        var collapsed = value.Replace("\n", " ").Replace("\r", " ").Trim();

        return collapsed.Length <= maxLength
            ? collapsed
            : collapsed[..(maxLength - 1)] + "…";
    }

    private static string EscapeInlineCode(string value)
    {
        return value.Replace("`", "'");
    }

    private static string EscapeMarkdown(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("*", "\\*")
            .Replace("_", "\\_")
            .Replace("~", "\\~")
            .Replace("`", "'");
    }
}
