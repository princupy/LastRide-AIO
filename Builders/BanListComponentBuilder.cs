using Discord;
using LastRide.Models;
using LastRide.Services;

namespace LastRide.Builders;

public sealed class BanListComponentBuilder
{
    private const int MaxNameLength = 22;
    private const int MaxReasonLength = 120;
    private static readonly Color AccentColor = ComponentTheme.AccentColor;

    public MessageComponent Build(
        IReadOnlyList<BannedUser> bans,
        string sessionId,
        ulong requesterId,
        int page)
    {
        if (bans.Count == 0)
        {
            return BuildNotice(
                "No Bans",
                "There are no banned members in this server.");
        }

        var pageSize = BanListService.PageSize;
        var totalPages = (bans.Count + pageSize - 1) / pageSize;
        page = Math.Clamp(page, 0, totalPages - 1);

        var start = page * pageSize;
        var pageBans = bans.Skip(start).Take(pageSize).ToArray();

        var components = new List<IMessageComponentBuilder>
        {
            new TextDisplayBuilder(
                $"## Ban List\n" +
                $"> Showing `{start + 1}`-`{start + pageBans.Length}` of `{bans.Count:N0}` banned member(s)." +
                $" Page `{page + 1}`/`{totalPages}`.")
        };

        foreach (var ban in pageBans)
        {
            components.Add(BuildUserSection(ban));
            components.Add(BuildUnbanRow(ban, sessionId, page));
            components.Add(Divider());
        }

        components.Add(BuildNavigationRow(sessionId, page, totalPages));
        components.Add(FooterSeparator());
        components.Add(new TextDisplayBuilder(ComponentFooter.Text));

        return BuildContainer(components.ToArray());
    }

    public MessageComponent BuildNotice(
        string title,
        string message)
    {
        return BuildContainer(
            new TextDisplayBuilder(
                $"## {EscapeMarkdown(title)}\n> {message}"));
    }

    private static SectionBuilder BuildUserSection(BannedUser ban)
    {
        var content =
            $"> **Member:** `{EscapeInlineCode(Truncate(ban.UserName, MaxNameLength))}`\n" +
            $"> **User ID:** `{ban.UserId}`\n" +
            $"> **Reason:** `{EscapeInlineCode(Truncate(ban.Reason, MaxReasonLength))}`";

        return new SectionBuilder()
            .WithAccessory(
                new ThumbnailBuilder(
                    new UnfurledMediaItemProperties(ban.AvatarUrl),
                    description: ban.UserName))
            .AddComponents(
                new TextDisplayBuilder(content));
    }

    private static ActionRowBuilder BuildUnbanRow(
        BannedUser ban,
        string sessionId,
        int page)
    {
        return new ActionRowBuilder()
            .WithButton(
                ButtonBuilder.CreateDangerButton(
                    $"Unban {Truncate(ban.UserName, MaxNameLength)}",
                    BanListComponentIds.CreateUnban(sessionId, page, ban.UserId)));
    }

    private static ActionRowBuilder BuildNavigationRow(
        string sessionId,
        int page,
        int totalPages)
    {
        return new ActionRowBuilder()
            .WithButton(
                ButtonBuilder.CreateSecondaryButton(
                    "Previous",
                    BanListComponentIds.CreateNav(
                        BanListAction.Previous,
                        sessionId,
                        page - 1))
                    .WithDisabled(page <= 0))
            .WithButton(
                ButtonBuilder.CreateDangerButton(
                    "Unban All",
                    BanListComponentIds.CreateUnbanAll(sessionId, page)))
            .WithButton(
                ButtonBuilder.CreateSecondaryButton(
                    "Next",
                    BanListComponentIds.CreateNav(
                        BanListAction.Next,
                        sessionId,
                        page + 1))
                    .WithDisabled(page >= totalPages - 1));
    }

    private static MessageComponent BuildContainer(
        params IMessageComponentBuilder[] components)
    {
        var container = new ContainerBuilder()
            .WithAccentColor(AccentColor)
            .AddComponents(components);

        return new ComponentBuilderV2()
            .AddComponent(container)
            .Build();
    }

    private static SeparatorBuilder Divider(bool isDivider = true)
    {
        return new SeparatorBuilder(
            isDivider: isDivider,
            spacing: SeparatorSpacingSize.Small);
    }

    private static SeparatorBuilder FooterSeparator()
    {
        return new SeparatorBuilder(
            isDivider: true,
            spacing: SeparatorSpacingSize.Small);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        value = value.Trim();

        return value.Length <= maxLength
            ? value
            : value[..(maxLength - 1)] + "…";
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
