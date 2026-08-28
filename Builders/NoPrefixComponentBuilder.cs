using Discord;
using LastRide.Models;

namespace LastRide.Builders;

/// <summary>
/// Renders the duration dropdown shown after <c>nop add</c>, the confirmation that
/// replaces it once a duration is picked, and the paginated listing of everyone who
/// currently holds no-prefix access.
/// </summary>
public sealed class NoPrefixComponentBuilder
{
    /// <summary>Members shown per page; each one costs a section plus a divider.</summary>
    public const int ListPageSize = 5;

    private const int MaxNameLength = 24;

    private static readonly Color AccentColor = ComponentTheme.AccentColor;

    /// <summary>
    /// The dropdown itself. Every duration lives in one menu so the owner picks the
    /// length in a single click instead of typing it, and the target travels inside
    /// the custom id rather than in any pending server-side state.
    /// </summary>
    public MessageComponent BuildDurationPrompt(IUser target, ulong requesterId)
    {
        var body =
            $"> **Member:** {target.Mention}\n" +
            $"> **User ID:** `{target.Id}`\n" +
            "> Pick how long this member may run commands without the prefix.";

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader(
                "No-Prefix Access",
                body,
                AvatarUrl(target),
                target.Username),
            Divider(),
            BuildDurationRow(target.Id, requesterId)
        };

        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    /// <summary>
    /// Replaces the dropdown card in place once a duration is picked, so the finished
    /// flow leaves one card behind instead of a stale menu plus a reply.
    /// </summary>
    public MessageComponent BuildGrantConfirmation(
        NoPrefixEntry entry,
        IUser? target,
        bool isPersistent)
    {
        var name = target is null
            ? $"`{entry.UserId}`"
            : $"`{EscapeInlineCode(Truncate(target.Username, MaxNameLength))}`";

        var expires = entry.IsPermanent
            ? "`Never`"
            : $"<t:{entry.ExpiresAt}:R> (<t:{entry.ExpiresAt}:f>)";

        var body =
            $"> **Member:** <@{entry.UserId}> ({name})\n" +
            $"> **User ID:** `{entry.UserId}`\n" +
            $"> **Duration:** `{entry.DurationLabel}`\n" +
            $"> **Expires:** {expires}";

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader(
                "No-Prefix Granted",
                body,
                target is null ? null : AvatarUrl(target),
                target?.Username ?? "Member"),
            Divider(),
            new TextDisplayBuilder(
                "### What Changed\n" +
                "> This member can now run commands without the prefix in every " +
                "server I am in.\n" +
                "> Permissions are untouched — every command still checks them as " +
                "usual.")
        };

        AppendPersistenceNote(components, isPersistent);
        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    /// <summary>
    /// One avatar section per member. Newest grant first, because the owner almost
    /// always wants to confirm what they just did.
    /// </summary>
    public MessageComponent BuildList(
        IReadOnlyList<NoPrefixEntry> entries,
        IReadOnlyDictionary<ulong, IUser?> users,
        int page,
        ulong requesterId)
    {
        if (entries.Count == 0)
        {
            return BuildNotice(
                "No Members",
                "Nobody has no-prefix access right now.");
        }

        var totalPages = Math.Max(1, (entries.Count + ListPageSize - 1) / ListPageSize);
        page = Math.Clamp(page, 0, totalPages - 1);

        var start = page * ListPageSize;
        var pageEntries = entries.Skip(start).Take(ListPageSize).ToArray();

        var components = new List<IMessageComponentBuilder>
        {
            new TextDisplayBuilder(
                "## No-Prefix Members\n" +
                $"> Showing `{start + 1}`-`{start + pageEntries.Length}` of " +
                $"`{entries.Count:N0}` member(s). Page `{page + 1}`/`{totalPages}`.")
        };

        for (var index = 0; index < pageEntries.Length; index++)
        {
            var entry = pageEntries[index];

            components.Add(BuildEntrySection(
                entry,
                users.TryGetValue(entry.UserId, out var user) ? user : null,
                start + index + 1));

            components.Add(Divider());
        }

        components.Add(BuildListNavigationRow(page, totalPages, requesterId));

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

    private static ActionRowBuilder BuildDurationRow(ulong targetId, ulong requesterId)
    {
        var menu = new SelectMenuBuilder()
            .WithCustomId(NoPrefixComponentIds.CreateDurationMenu(targetId, requesterId))
            .WithPlaceholder("Select how long the access lasts")
            .WithMinValues(1)
            .WithMaxValues(1);

        foreach (var duration in Enum.GetValues<NoPrefixDuration>())
        {
            menu.AddOption(
                NoPrefixComponentIds.ToLabel(duration),
                NoPrefixComponentIds.ToValue(duration),
                MenuHint(duration));
        }

        return new ActionRowBuilder().WithSelectMenu(menu);
    }

    private static string MenuHint(NoPrefixDuration duration)
    {
        return duration switch
        {
            NoPrefixDuration.OneDay => "Expires after 1 day",
            NoPrefixDuration.FifteenDays => "Expires after 15 days",
            NoPrefixDuration.ThirtyDays => "Expires after 30 days",
            NoPrefixDuration.ThreeMonths => "Expires after 90 days",
            NoPrefixDuration.OneYear => "Expires after 365 days",
            _ => "Never expires until removed"
        };
    }

    private static IMessageComponentBuilder BuildEntrySection(
        NoPrefixEntry entry,
        IUser? user,
        int position)
    {
        var expires = entry.IsPermanent
            ? "`Permanent`"
            : $"<t:{entry.ExpiresAt}:R>";

        // A member I have never seen keeps their row but has no avatar to hang a
        // thumbnail on, so it degrades to plain text.
        if (user is null)
        {
            return new TextDisplayBuilder(
                $"> **#{position}** <@{entry.UserId}>\n" +
                $"> **User ID:** `{entry.UserId}`\n" +
                $"> **Duration:** `{entry.DurationLabel}` • **Expires:** {expires}\n" +
                $"> **Granted:** <t:{entry.GrantedAt}:R> by <@{entry.GrantedBy}>");
        }

        var content =
            $"> **#{position}** {user.Mention}\n" +
            $"> **Member:** `{EscapeInlineCode(Truncate(user.Username, MaxNameLength))}`\n" +
            $"> **User ID:** `{entry.UserId}`\n" +
            $"> **Duration:** `{entry.DurationLabel}` • **Expires:** {expires}\n" +
            $"> **Granted:** <t:{entry.GrantedAt}:R> by <@{entry.GrantedBy}>";

        return new SectionBuilder()
            .WithAccessory(
                new ThumbnailBuilder(
                    new UnfurledMediaItemProperties(AvatarUrl(user)),
                    description: user.Username))
            .AddComponents(new TextDisplayBuilder(content));
    }

    private static ActionRowBuilder BuildListNavigationRow(
        int page,
        int totalPages,
        ulong requesterId)
    {
        return new ActionRowBuilder()
            .WithButton(
                ButtonBuilder.CreateSecondaryButton(
                    "Previous",
                    NoPrefixComponentIds.CreateListNav(page - 1, requesterId))
                    .WithDisabled(page <= 0))
            .WithButton(
                ButtonBuilder.CreateSecondaryButton(
                    "Next",
                    NoPrefixComponentIds.CreateListNav(page + 1, requesterId))
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
            "-# Note: this is active now but will be lost when the bot restarts."));
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

    private static string AvatarUrl(IUser user)
    {
        return user.GetDisplayAvatarUrl(size: 256) ?? user.GetDefaultAvatarUrl();
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
