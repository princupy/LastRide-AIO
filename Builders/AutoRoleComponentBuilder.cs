using Discord;

namespace LastRide.Builders;

public sealed class AutoRoleComponentBuilder
{
    private const string EnabledEmoji = "<:Enabled:1541831759191212154>";
    private const string DisabledEmoji = "<:disabled:1541831823406014699>";

    private static readonly Color AccentColor = new(8, 4, 4);

    public MessageComponent BuildAutoRoleStatus(
        bool enabled,
        IReadOnlyCollection<ulong> humanRoleIds,
        IReadOnlyCollection<ulong> botRoleIds,
        int maxPerType,
        string? note,
        bool isPersistent,
        string prefix)
    {
        var emoji = enabled ? EnabledEmoji : DisabledEmoji;

        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(note))
            lines.Add($"> {note}");

        lines.Add($"> {emoji} **Status:** {(enabled ? "Enabled" : "Disabled")}");
        lines.Add($"> **Human Roles ({humanRoleIds.Count}/{maxPerType}):** {FormatRoles(humanRoleIds)}");
        lines.Add($"> **Bot Roles ({botRoleIds.Count}/{maxPerType}):** {FormatRoles(botRoleIds)}");
        lines.Add(
            $"-# `{prefix}autorole add @role` • `{prefix}autorole humans/bots @role` • `{prefix}autorole remove @role` • `{prefix}autorole on/off`");

        return BuildStatusCard($"{emoji} Autorole", lines, isPersistent);
    }

    public MessageComponent BuildVcRoleStatus(
        bool enabled,
        ulong? roleId,
        string? note,
        bool isPersistent,
        string prefix)
    {
        var emoji = enabled ? EnabledEmoji : DisabledEmoji;

        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(note))
            lines.Add($"> {note}");

        lines.Add($"> {emoji} **Status:** {(enabled ? "Enabled" : "Disabled")}");
        lines.Add(roleId is { } id
            ? $"> **Role:** <@&{id}>"
            : "> **Role:** `Not set`");
        lines.Add(
            $"-# `{prefix}vcrole set @role` • `{prefix}vcrole remove` • `{prefix}vcrole on/off`");

        return BuildStatusCard($"{emoji} VC Role", lines, isPersistent);
    }

    public MessageComponent BuildNotice(string title, string message)
    {
        return BuildContainer(
            new TextDisplayBuilder(
                $"## {EscapeMarkdown(title)}\n> {message}"));
    }

    private static string FormatRoles(IReadOnlyCollection<ulong> roleIds)
    {
        return roleIds.Count == 0
            ? "`None`"
            : string.Join(", ", roleIds.Select(id => $"<@&{id}>"));
    }

    private MessageComponent BuildStatusCard(
        string title,
        List<string> lines,
        bool isPersistent)
    {
        var components = new List<IMessageComponentBuilder>
        {
            new TextDisplayBuilder(
                $"## {EscapeMarkdown(title)}\n{string.Join("\n", lines)}")
        };

        AppendPersistenceNote(components, isPersistent);
        AppendFooter(components);

        return BuildContainer(components.ToArray());
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
        components.Add(new SeparatorBuilder(
            isDivider: true,
            spacing: SeparatorSpacingSize.Small));
        components.Add(new TextDisplayBuilder(ComponentFooter.Text));
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
