using Discord;
using Discord.WebSocket;

namespace LastRide.Builders;

public sealed class RoleInfoComponentBuilder
{
    private static readonly Color AccentColor = ComponentTheme.AccentColor;

    public MessageComponent BuildMissingRole()
    {
        var container = new ContainerBuilder()
            .WithAccentColor(AccentColor)
            .AddComponent(
                new TextDisplayBuilder(
                    "Please mention a role or provide a valid role name/id."));

        return new ComponentBuilderV2()
            .AddComponent(container)
            .Build();
    }

    public MessageComponent Build(
        SocketRole role,
        ulong requesterId,
        RoleInfoPage selectedPage = RoleInfoPage.Info)
    {
        var iconUrl = role.GetIconUrl();
        var title = selectedPage == RoleInfoPage.Members
            ? $"## {EscapeMarkdown(role.Name)} Members"
            : $"## {EscapeMarkdown(role.Name)} Role Info";
        var content = selectedPage == RoleInfoPage.Members
            ? BuildMemberList(role)
            : BuildInfoContent(role);

        var container = new ContainerBuilder()
            .WithAccentColor(AccentColor);

        if (!string.IsNullOrWhiteSpace(iconUrl))
        {
            container.AddComponent(
                new SectionBuilder()
                    .WithAccessory(
                        new ThumbnailBuilder(
                            new UnfurledMediaItemProperties(iconUrl),
                            description: role.Name))
                    .AddComponents(
                        new TextDisplayBuilder(title),
                        new TextDisplayBuilder(content)));
        }
        else
        {
            container.AddComponents(
                new TextDisplayBuilder(title),
                new TextDisplayBuilder(content));
        }

        container.AddComponents(
            Divider(isDivider: false),
            BuildButtons(role, requesterId, selectedPage),
            FooterSeparator(),
            new TextDisplayBuilder(ComponentFooter.Text));

        return new ComponentBuilderV2()
            .AddComponent(container)
            .Build();
    }

    private static string BuildInfoContent(SocketRole role)
    {
        return
            BuildIdentity(role) +
            "\n\n" +
            BuildMembers(role) +
            "\n\n" +
            BuildPermissions(role) +
            "\n\n" +
            BuildManagement(role);
    }

    private static string BuildIdentity(SocketRole role)
    {
        return
            "### Identity\n" +
            $"> **Role:** `@{EscapeInlineCode(role.Name)}`\n" +
            $"> **Name:** `{EscapeInlineCode(role.Name)}`\n" +
            $"> **Role ID:** `{role.Id}`\n" +
            $"> **Created:** {FormatTimestamp(role.CreatedAt)}\n" +
            $"> **Created Ago:** {FormatRelative(role.CreatedAt)}\n" +
            $"> **Position:** `{role.Position:N0}`\n" +
            $"> **Color:** `{FormatColor(role.Color)}`\n" +
            $"> **Icon:** {FormatLink(role.GetIconUrl())}\n" +
            $"> **Emoji:** `{EscapeInlineCode(role.Emoji?.ToString() ?? "Not set")}`";
    }

    private static string BuildMembers(SocketRole role)
    {
        var members = role.Members.ToArray();
        var humans = members.Count(member => !member.IsBot);
        var bots = members.Count(member => member.IsBot);

        return
            "### Members\n" +
            $"> **Members With Role:** `{members.Length:N0}`\n" +
            $"> **Humans:** `{humans:N0}`\n" +
            $"> **Bots:** `{bots:N0}`\n" +
            $"> **Hoisted:** `{FormatBool(role.IsHoisted)}`\n" +
            $"> **Mentionable:** `{FormatBool(role.IsMentionable)}`";
    }

    private static string BuildMemberList(SocketRole role)
    {
        var members = role.Members
            .OrderBy(member => member.DisplayName)
            .ToArray();

        if (members.Length == 0)
        {
            return
                "### Members\n" +
                "> No cached members currently have this role.";
        }

        var lines = new List<string>
        {
            "### Members",
            $"> **Total:** `{members.Length:N0}`",
            ""
        };

        var shown = 0;

        foreach (var member in members)
        {
            var line =
                $"> `{EscapeInlineCode(member.DisplayName)}` " +
                $"(`{EscapeInlineCode(member.Username)}`, `{member.Id}`)";

            var nextLength = lines.Sum(value => value.Length + 1) + line.Length;

            if (nextLength > 3400)
                break;

            lines.Add(line);
            shown++;
        }

        if (shown < members.Length)
        {
            lines.Add($"> `+{members.Length - shown:N0} more members`");
        }

        return string.Join('\n', lines);
    }

    private static string BuildPermissions(SocketRole role)
    {
        var permissions = FormatAllPermissions(role.Permissions);

        return
            "### Permissions\n" +
            $"> **Administrator:** `{FormatBool(role.Permissions.Administrator)}`\n" +
            $"> **Permissions:** {permissions}\n" +
            $"> **Raw Value:** `{role.Permissions.RawValue}`";
    }

    private static string BuildManagement(SocketRole role)
    {
        var tags = role.Tags;

        return
            "### Management\n" +
            $"> **Managed:** `{FormatBool(role.IsManaged)}`\n" +
            $"> **Everyone Role:** `{FormatBool(role.IsEveryone)}`\n" +
            $"> **Bot Managed:** `{FormatOptionalId(tags.BotId)}`\n" +
            $"> **Integration Managed:** `{FormatOptionalId(tags.IntegrationId)}`\n" +
            $"> **Booster Role:** `{FormatBool(tags.IsPremiumSubscriberRole)}`\n" +
            $"> **Linked Role:** `{FormatBool(tags.IsGuildConnection)}`\n" +
            $"> **Available For Purchase:** `{FormatBool(tags.IsAvailableForPurchase)}`";
    }

    private static string FormatAllPermissions(GuildPermissions permissions)
    {
        var values = typeof(GuildPermissions)
            .GetProperties()
            .Where(property => property.PropertyType == typeof(bool))
            .Where(property => (bool)(property.GetValue(permissions) ?? false))
            .Select(property => HumanizePermissionName(property.Name))
            .OrderBy(value => value)
            .ToArray();

        if (values.Length == 0)
            return "`None`";

        return string.Join(
            ", ",
            values
                .Select(value => $"`{EscapeInlineCode(value)}`"));
    }

    private static string HumanizePermissionName(string value)
    {
        return string.Concat(
            value.SelectMany((character, index) =>
                index > 0 && char.IsUpper(character)
                    ? new[] { ' ', character }
                    : new[] { character }));
    }

    private static ActionRowBuilder BuildButtons(
        SocketRole role,
        ulong requesterId,
        RoleInfoPage selectedPage)
    {
        return new ActionRowBuilder()
            .WithButton(
                ButtonBuilder
                    .CreateSecondaryButton(
                        "Role Info",
                        RoleInfoComponentIds.Create(
                            RoleInfoPage.Info,
                            requesterId,
                            role.Guild.Id,
                            role.Id))
                    .WithDisabled(selectedPage == RoleInfoPage.Info))
            .WithButton(
                ButtonBuilder
                    .CreateSecondaryButton(
                        "Members",
                        RoleInfoComponentIds.Create(
                            RoleInfoPage.Members,
                            requesterId,
                            role.Guild.Id,
                            role.Id))
                    .WithDisabled(selectedPage == RoleInfoPage.Members));
    }

    private static string FormatColor(Color color)
    {
        return $"#{color.RawValue:X6} ({color.R}, {color.G}, {color.B})";
    }

    private static string FormatLink(string? url)
    {
        return string.IsNullOrWhiteSpace(url)
            ? "`Not set`"
            : $"[Open Link]({url})";
    }

    private static string FormatBool(bool value)
    {
        return value ? "Yes" : "No";
    }

    private static string FormatOptionalId(ulong? id)
    {
        return id.HasValue ? id.Value.ToString() : "No";
    }

    private static string FormatTimestamp(DateTimeOffset timestamp)
    {
        return $"<t:{timestamp.ToUnixTimeSeconds()}:F>";
    }

    private static string FormatRelative(DateTimeOffset timestamp)
    {
        return $"<t:{timestamp.ToUnixTimeSeconds()}:R>";
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
