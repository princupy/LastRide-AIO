using Discord;
using Discord.WebSocket;
using LastRide.Models;

namespace LastRide.Builders;

/// <summary>
/// Renders the giveaway card and its enter button, the winner announcement, the
/// paginated entry list, the running-giveaway listing, and the DM sent back when a
/// winner is forced.
/// </summary>
public sealed class GiveawayComponentBuilder
{
    /// <summary>Entries shown per page; each one costs a section plus a divider.</summary>
    public const int EntriesPageSize = 5;

    // A single card cannot hold 25 giveaways without overflowing the container, so
    // the listing shows the soonest ones and counts the rest.
    private const int MaxListedGiveaways = 10;

    private const int MaxPrizeLength = 200;
    private const int MaxListedPrizeLength = 80;
    private const int MaxNameLength = 24;

    private static readonly Color AccentColor = ComponentTheme.AccentColor;

    /// <summary>
    /// The giveaway card itself, in both states. The countdown is Discord's native
    /// relative timestamp, so the clock ticks client-side and the message never has
    /// to be edited just to move the timer.
    /// </summary>
    public MessageComponent BuildCard(Giveaway giveaway, SocketGuild guild)
    {
        var lines = new List<string>();

        if (giveaway.IsEnded)
        {
            lines.Add(giveaway.WinnerIds.Count == 0
                ? "> **Winner:** `Nobody eligible entered.`"
                : $"> **Winner(s):** {FormatMentions(giveaway.WinnerIds)}");

            lines.Add($"> **Ended:** <t:{giveaway.EndsAt}:R>");
        }
        else
        {
            lines.Add("> Press the button below to enter.");
            lines.Add($"> **Ends:** <t:{giveaway.EndsAt}:R> (<t:{giveaway.EndsAt}:f>)");
            lines.Add($"> **Winners:** `{giveaway.WinnerCount}`");
        }

        lines.Add($"> **Entries:** `{giveaway.EntryIds.Count:N0}`");
        lines.Add($"> **Hosted by:** <@{giveaway.HostId}>");

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader(
                $"🎉 {Truncate(giveaway.Prize, MaxPrizeLength)}",
                string.Join("\n", lines),
                guild.IconUrl,
                guild.Name),
            Divider(),
            BuildEnterRow(giveaway)
        };

        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    /// <summary>
    /// Posted as a fresh message when a giveaway is drawn, so the winners get a
    /// real notification instead of a silent edit to an older card.
    /// </summary>
    public MessageComponent BuildAnnouncement(
        Giveaway giveaway,
        SocketGuild guild,
        bool isReroll)
    {
        if (giveaway.WinnerIds.Count == 0)
        {
            var empty =
                $"> **Prize:** {EscapeMarkdown(Truncate(giveaway.Prize, MaxPrizeLength))}\n" +
                $"> **Entries:** `{giveaway.EntryIds.Count:N0}`\n" +
                (isReroll
                    ? "> Everyone who entered has already won this giveaway."
                    : "> Nobody eligible entered, so there is nothing to award.") +
                $"\n> [Jump to giveaway]({giveaway.JumpUrl})";

            var noWinnerComponents = new List<IMessageComponentBuilder>
            {
                BuildHeader("😔 No Winners", empty, guild.IconUrl, guild.Name)
            };

            AppendFooter(noWinnerComponents);
            return BuildContainer(noWinnerComponents.ToArray());
        }

        var body =
            $"> **Prize:** {EscapeMarkdown(Truncate(giveaway.Prize, MaxPrizeLength))}\n" +
            $"> **Winner(s):** {FormatMentions(giveaway.WinnerIds)}\n" +
            $"> **Entries:** `{giveaway.EntryIds.Count:N0}`\n" +
            $"> [Jump to giveaway]({giveaway.JumpUrl})";

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader(
                isReroll ? "🎉 Giveaway Rerolled" : "🎉 Giveaway Ended",
                body,
                guild.IconUrl,
                guild.Name),
            Divider(),
            new TextDisplayBuilder(
                $"### Congratulations\n> {FormatMentions(giveaway.WinnerIds)} — " +
                "reach out to the host to claim your prize.")
        };

        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    /// <summary>
    /// One avatar section per entrant. Ordered by id rather than insertion order,
    /// because the stored set gives no reliable ordering and a page flip has to land
    /// on the same rows every time.
    /// </summary>
    public MessageComponent BuildEntries(
        Giveaway giveaway,
        SocketGuild guild,
        int page,
        ulong requesterId)
    {
        if (giveaway.EntryIds.Count == 0)
        {
            return BuildNotice(
                "No Entries",
                "Nobody has entered this giveaway yet.");
        }

        var entries = giveaway.EntryIds.OrderBy(entryId => entryId).ToArray();
        var totalPages = Math.Max(1, (entries.Length + EntriesPageSize - 1) / EntriesPageSize);
        page = Math.Clamp(page, 0, totalPages - 1);

        var start = page * EntriesPageSize;
        var pageEntries = entries.Skip(start).Take(EntriesPageSize).ToArray();

        var components = new List<IMessageComponentBuilder>
        {
            new TextDisplayBuilder(
                $"## Giveaway Entries\n" +
                $"> **Prize:** {EscapeMarkdown(Truncate(giveaway.Prize, MaxListedPrizeLength))}\n" +
                $"> Showing `{start + 1}`-`{start + pageEntries.Length}` of " +
                $"`{entries.Length:N0}` entry(s). Page `{page + 1}`/`{totalPages}`.")
        };

        for (var index = 0; index < pageEntries.Length; index++)
        {
            components.Add(BuildEntrySection(
                guild,
                pageEntries[index],
                start + index + 1));

            components.Add(Divider());
        }

        components.Add(BuildEntriesNavigationRow(
            giveaway.MessageId,
            page,
            totalPages,
            requesterId));

        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    /// <summary>Running giveaways with jump links, soonest first.</summary>
    public MessageComponent BuildList(
        IReadOnlyList<Giveaway> giveaways,
        SocketGuild guild,
        string prefix)
    {
        if (giveaways.Count == 0)
        {
            return BuildNotice(
                "No Giveaways",
                $"No giveaway is running here — start one with `{prefix}gstart 1h Prize`.");
        }

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader(
                "Running Giveaways",
                $"> `{giveaways.Count}` giveaway(s) are live in this server.",
                guild.IconUrl,
                guild.Name),
            Divider()
        };

        foreach (var giveaway in giveaways.Take(MaxListedGiveaways))
        {
            components.Add(new TextDisplayBuilder(
                $"### 🎉 {EscapeMarkdown(Truncate(giveaway.Prize, MaxListedPrizeLength))}\n" +
                $"> **Ends:** <t:{giveaway.EndsAt}:R> • **Winners:** " +
                $"`{giveaway.WinnerCount}` • **Entries:** " +
                $"`{giveaway.EntryIds.Count:N0}`\n" +
                $"> **Channel:** <#{giveaway.ChannelId}> • " +
                $"[Jump]({giveaway.JumpUrl}) • ID `{giveaway.MessageId}`"));

            components.Add(Divider());
        }

        if (giveaways.Count > MaxListedGiveaways)
        {
            components.Add(new TextDisplayBuilder(
                $"-# `{giveaways.Count - MaxListedGiveaways}` more not shown."));
        }

        components.Add(new TextDisplayBuilder(
            $"-# `{prefix}gentries <id>` lists who entered • " +
            $"`{prefix}gend <id>` ends one early"));

        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    /// <summary>
    /// Sent by DM only, so the rig leaves nothing behind in the channel. Spells out
    /// that the force is single-use, which is the part that is easy to forget.
    /// </summary>
    public MessageComponent BuildRigConfirmation(
        Giveaway giveaway,
        SocketGuild guild,
        ulong? targetId)
    {
        var body =
            $"> **Server:** {EscapeMarkdown(guild.Name)}\n" +
            $"> **Prize:** {EscapeMarkdown(Truncate(giveaway.Prize, MaxPrizeLength))}\n" +
            $"> **Winners:** `{giveaway.WinnerCount}` • **Entries:** " +
            $"`{giveaway.EntryIds.Count:N0}`\n" +
            $"> [Jump to giveaway]({giveaway.JumpUrl})";

        var detail = targetId is { } userId
            ? $"### Forced Winner\n" +
              $"> <@{userId}> will take the first winner slot on the next draw, " +
              "whether or not they pressed enter.\n" +
              "> This is used up by that draw — a reroll afterwards picks randomly " +
              "and can never land on them again.\n" +
              "> The command message was deleted from the channel."
            : "### Force Cleared\n" +
              "> No winner is forced any more; the next draw is fully random.\n" +
              "> The command message was deleted from the channel.";

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader(
                targetId is null ? "Winner Force Cleared" : "Winner Set",
                body,
                guild.IconUrl,
                guild.Name),
            Divider(),
            new TextDisplayBuilder(detail)
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

    private static ActionRowBuilder BuildEnterRow(Giveaway giveaway)
    {
        return new ActionRowBuilder()
            .WithButton(
                ButtonBuilder
                    .CreatePrimaryButton(
                        "Enter",
                        GiveawayComponentIds.CreateEnter(giveaway.MessageId))
                    .WithEmote(new Emoji("🎉"))
                    .WithDisabled(giveaway.IsEnded));
    }

    private static IMessageComponentBuilder BuildEntrySection(
        SocketGuild guild,
        ulong entryId,
        int position)
    {
        var member = guild.GetUser(entryId);

        // A member who left keeps their slot in the count but has no avatar to
        // hang a thumbnail on, so their row degrades to plain text.
        if (member is null)
        {
            return new TextDisplayBuilder(
                $"> **#{position}** <@{entryId}>\n" +
                $"> **User ID:** `{entryId}`\n" +
                "> *Left the server — not eligible to win.*");
        }

        var content =
            $"> **#{position}** {member.Mention}\n" +
            $"> **Member:** `{EscapeInlineCode(Truncate(member.Username, MaxNameLength))}`\n" +
            $"> **User ID:** `{entryId}`";

        return new SectionBuilder()
            .WithAccessory(
                new ThumbnailBuilder(
                    new UnfurledMediaItemProperties(AvatarUrl(member)),
                    description: member.DisplayName))
            .AddComponents(new TextDisplayBuilder(content));
    }

    private static ActionRowBuilder BuildEntriesNavigationRow(
        ulong messageId,
        int page,
        int totalPages,
        ulong requesterId)
    {
        return new ActionRowBuilder()
            .WithButton(
                ButtonBuilder.CreateSecondaryButton(
                    "Previous",
                    GiveawayComponentIds.CreateEntriesNav(
                        messageId,
                        page - 1,
                        requesterId))
                    .WithDisabled(page <= 0))
            .WithButton(
                ButtonBuilder.CreateSecondaryButton(
                    "Next",
                    GiveawayComponentIds.CreateEntriesNav(
                        messageId,
                        page + 1,
                        requesterId))
                    .WithDisabled(page >= totalPages - 1));
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
            "-# Note: this giveaway is active now but will be lost when the bot restarts."));
    }

    private static void AppendFooter(List<IMessageComponentBuilder> components)
    {
        components.Add(FooterSeparator());
        components.Add(new TextDisplayBuilder(ComponentFooter.Text));
    }

    private static SeparatorBuilder Divider()
    {
        return new SeparatorBuilder(isDivider: true, spacing: SeparatorSpacingSize.Small);
    }

    private static SeparatorBuilder FooterSeparator()
    {
        return new SeparatorBuilder(isDivider: true, spacing: SeparatorSpacingSize.Small);
    }

    private static string FormatMentions(IReadOnlyCollection<ulong> userIds)
    {
        return string.Join(" ", userIds.Select(userId => $"<@{userId}>"));
    }

    private static string AvatarUrl(SocketGuildUser member)
    {
        return member.GetDisplayAvatarUrl(size: 256) ?? member.GetDefaultAvatarUrl();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var trimmed = value.Trim();

        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..(maxLength - 1)] + "…";
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
