using Discord;
using LastRide.Models;
using LastRide.Services;

namespace LastRide.Builders;

public sealed class SetupRoleComponentBuilder
{
    private const int MaxListedStaffRoles = 20;
    private const int MaxListedCommands = 25;

    private static readonly Color AccentColor = ComponentTheme.AccentColor;

    public MessageComponent BuildRoleToggled(
        bool added,
        ulong targetId,
        string targetName,
        string? targetAvatarUrl,
        string commandName,
        string prefix,
        string roleName,
        ulong roleId,
        ulong moderatorId)
    {
        var content =
            $"> **Member:** <@{targetId}>\n" +
            $"> **Role:** `{EscapeInlineCode(roleName)}`\n" +
            $"> **Role ID:** `{roleId}`\n" +
            $"> **Command:** `{prefix}{EscapeInlineCode(commandName)}`\n" +
            $"> **Moderator:** <@{moderatorId}>";

        return BuildContainer(
            BuildHeader(
                added ? "Role Added" : "Role Removed",
                content,
                targetAvatarUrl,
                targetName));
    }

    public MessageComponent BuildStaffRoleList(
        SetupRoleConfig config,
        string prefix,
        bool isPersistent)
    {
        var lines = new List<string>
        {
            config.StaffRoleIds.Count == 0
                ? "> No staff roles configured — only administrators can use " +
                  "dynamic role commands."
                : $"> `{config.StaffRoleIds.Count}`/" +
                  $"`{SetupRoleConfigService.MaxStaffRoles}` staff role(s) configured."
        };

        if (config.StaffRoleIds.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("### Staff Roles");
            lines.Add(FormatMentionList(
                config.StaffRoleIds,
                MaxListedStaffRoles,
                id => $"<@&{id}>"));
        }

        lines.Add(string.Empty);
        lines.Add(
            $"-# `{prefix}setuprole add @role` • " +
            $"`{prefix}setuprole remove @role`");

        return BuildStatusCard("Setup-Roles Staff Access", lines, isPersistent);
    }

    public MessageComponent BuildCommandList(
        SetupRoleConfig config,
        string prefix,
        bool isPersistent)
    {
        var lines = new List<string>
        {
            config.Commands.Count == 0
                ? "> No role commands created yet."
                : $"> `{config.Commands.Count}`/" +
                  $"`{SetupRoleConfigService.MaxCommands}` command(s) created."
        };

        if (config.Commands.Count > 0)
        {
            var ordered = config.OrderedCommands.ToArray();

            lines.Add(string.Empty);
            lines.Add("### Role Commands");

            foreach (var pair in ordered.Take(MaxListedCommands))
            {
                lines.Add(
                    $"> `{prefix}{EscapeInlineCode(pair.Key)}` — <@&{pair.Value}>");
            }

            if (ordered.Length > MaxListedCommands)
                lines.Add($"> …and `{ordered.Length - MaxListedCommands}` more.");
        }

        lines.Add(string.Empty);
        lines.Add(
            $"-# `{prefix}setuprolecreate <name> @role` to create • " +
            $"`{prefix}setuprolecreate remove <name>` to delete");
        lines.Add(
            "-# Running a role command toggles the role — it is added when the " +
            "member does not have it, removed when they do.");

        return BuildStatusCard("Setup-Roles Commands", lines, isPersistent);
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

    private static MessageComponent BuildStatusCard(
        string title,
        List<string> lines,
        bool isPersistent)
    {
        var components = new List<IMessageComponentBuilder>
        {
            new TextDisplayBuilder($"## {EscapeMarkdown(title)}\n{string.Join("\n", lines)}")
        };

        AppendPersistenceNote(components, isPersistent);
        AppendFooter(components);
        return BuildContainer(components.ToArray());
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

    private static SeparatorBuilder FooterSeparator()
    {
        return new SeparatorBuilder(isDivider: true, spacing: SeparatorSpacingSize.Small);
    }

    private static string FormatMentionList(
        IEnumerable<ulong> ids,
        int maxListed,
        Func<ulong, string> format)
    {
        var all = ids.ToArray();

        var mentions = all
            .Take(maxListed)
            .Select(format)
            .ToList();

        var rendered = $"> {string.Join(", ", mentions)}";

        if (all.Length > maxListed)
            rendered += $"\n> …and `{all.Length - maxListed}` more.";

        return rendered;
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
