using Discord;

namespace LastRide.Builders;

public sealed class MentionComponentBuilder
{
    private const string ArrowEmoji = "<:ArrowRight:1541407020257640470>";
    private static readonly Color AccentColor = new(8, 4, 4);

    public MessageComponent Build(
        string botName,
        string botAvatarUrl,
        string prefix,
        int commandCount)
    {
        var description =
            $"Hey there — I'm **{EscapeInlineCode(botName)}**, your all-in-one server companion.\n" +
            "Packed with powerful moderation, utility, automation, and server management features — everything you need in one bot.";

        var summary =
            $"> {ArrowEmoji} **Server Prefix:** `{prefix}`\n" +
            $"> {ArrowEmoji} **Commands Loaded:** `{commandCount:N0}`\n" +
            $"> {ArrowEmoji} **Get Started:** `{prefix}help`";

        var section = new SectionBuilder()
            .WithAccessory(
                new ThumbnailBuilder(
                    new UnfurledMediaItemProperties(botAvatarUrl),
                    description: botName))
            .AddComponents(
                new TextDisplayBuilder(description));

        var container = new ContainerBuilder()
            .WithAccentColor(AccentColor)
            .AddComponents(
                section,
                Divider(),
                new TextDisplayBuilder(summary),
                FooterSeparator(),
                new TextDisplayBuilder(ComponentFooter.Text));

        return new ComponentBuilderV2()
            .AddComponent(container)
            .Build();
    }

    private static SeparatorBuilder Divider()
    {
        return new SeparatorBuilder(
            isDivider: true,
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
}
