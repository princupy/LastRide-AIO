using Discord;
using LastRide.Models;

namespace LastRide.Builders;

public sealed class AfkComponentBuilder
{
    private static readonly Color AccentColor = new(8, 4, 4);

    public MessageComponent BuildSet(AfkStatus status)
    {
        var content =
            "## AFK Enabled\n" +
            $"> **User:** `{EscapeInlineCode(status.DisplayName)}`\n" +
            $"> **Reason:** {status.Reason}\n" +
            $"> **Since:** {FormatTimestamp(status.StartedAt)}\n" +
            $"> **Started:** {FormatExactTimestamp(status.StartedAt)}";

        return Build(content);
    }

    public MessageComponent BuildWelcomeBack(AfkStatus status)
    {
        var content =
            "## Welcome Back\n" +
            $"> **AFK Duration:** `{FormatDuration(DateTimeOffset.UtcNow - status.StartedAt)}`\n" +
            $"> **Since:** {FormatTimestamp(status.StartedAt)}\n" +
            $"> **Started:** {FormatExactTimestamp(status.StartedAt)}";

        return Build(content);
    }

    public MessageComponent BuildMentionNotice(
        IReadOnlyCollection<AfkStatus> statuses)
    {
        var entries = statuses.Select(status =>
            $"### {EscapeInlineCode(status.DisplayName)} is AFK\n" +
            $"> **Reason:** {status.Reason}\n" +
            $"> **Away For:** `{FormatDuration(DateTimeOffset.UtcNow - status.StartedAt)}`\n" +
            $"> **Since:** {FormatTimestamp(status.StartedAt)}\n" +
            $"> **Started:** {FormatExactTimestamp(status.StartedAt)}");

        return Build("## AFK Notice\n" + string.Join("\n", entries));
    }

    private static MessageComponent Build(string content)
    {
        var container = new ContainerBuilder()
            .WithAccentColor(AccentColor)
            .AddComponents(
                new TextDisplayBuilder(content),
                FooterSeparator(),
                new TextDisplayBuilder(ComponentFooter.Text));

        return new ComponentBuilderV2()
            .AddComponent(container)
            .Build();
    }

    private static SeparatorBuilder FooterSeparator()
    {
        return new SeparatorBuilder(
            isDivider: true,
            spacing: SeparatorSpacingSize.Small);
    }

    private static string FormatTimestamp(DateTimeOffset timestamp)
    {
        return $"<t:{timestamp.ToUnixTimeSeconds()}:R>";
    }

    private static string FormatExactTimestamp(DateTimeOffset timestamp)
    {
        return $"<t:{timestamp.ToUnixTimeSeconds()}:F>";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 1)
            return "just now";

        var parts = new List<string>();

        if (duration.Days > 0)
            parts.Add($"{duration.Days}d");

        if (duration.Hours > 0)
            parts.Add($"{duration.Hours}h");

        if (duration.Minutes > 0)
            parts.Add($"{duration.Minutes}m");

        if (duration.Seconds > 0 && parts.Count < 2)
            parts.Add($"{duration.Seconds}s");

        return string.Join(" ", parts.Take(2));
    }

    private static string EscapeInlineCode(string value)
    {
        return value.Replace("`", "'");
    }
}
