using Discord;

namespace LastRide.Builders;

public sealed class AutoResponderComponentBuilder
{
    private static readonly Color AccentColor = ComponentTheme.AccentColor;

    public MessageComponent BuildResponderList(
        IReadOnlyDictionary<string, string> responses,
        int maxResponders,
        bool isPersistent,
        string prefix)
    {
        var lines = new List<string>
        {
            $"> **Total:** `{responses.Count}/{maxResponders}`"
        };

        if (responses.Count == 0)
        {
            lines.Add("> No autoresponders yet. Add one with the command below.");
        }
        else
        {
            var index = 1;

            foreach (var pair in responses)
            {
                lines.Add(
                    $"> **{index}.** `{CodePreview(pair.Key, 60)}` → {Preview(pair.Value, 80)}");
                index++;
            }
        }

        lines.Add(
            $"-# `{prefix}autoresponder add <trigger> <reply>` • `{prefix}autoresponder edit <trigger> <reply>` • `{prefix}autoresponder remove <trigger>` • `{prefix}autoresponder list`");

        return BuildStatusCard("Autoresponder", lines, isPersistent);
    }

    public MessageComponent BuildNotice(string title, string message)
    {
        return BuildContainer(
            new TextDisplayBuilder(
                $"## {EscapeMarkdown(title)}\n> {message}"));
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

    // Shortens a value for display inside a fenced code span: collapses runs of
    // whitespace to single spaces and neutralises backticks so the span never
    // breaks. No backslash escaping — backslashes are literal inside code.
    private static string CodePreview(string value, int max)
    {
        var collapsed = Collapse(value);

        if (collapsed.Length > max)
            collapsed = collapsed[..max] + "…";

        return collapsed.Replace("`", "'");
    }

    // Shortens a value for display as plain text and escapes markdown so a
    // reply's formatting characters render literally in the card.
    private static string Preview(string value, int max)
    {
        var collapsed = Collapse(value);

        if (collapsed.Length > max)
            collapsed = collapsed[..max] + "…";

        return EscapeMarkdown(collapsed);
    }

    private static string Collapse(string value)
    {
        return string.Join(
            ' ',
            value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
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
