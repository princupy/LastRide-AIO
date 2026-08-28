using Discord;

namespace LastRide.Builders;

public sealed class PingComponentBuilder
{
    public MessageComponent Build(
        long apiLatency,
        string databaseLatency,
        string botAvatarUrl)
    {
        var thumbnail = new ThumbnailBuilder(
            new UnfurledMediaItemProperties(botAvatarUrl),
            description: "LastRide");

        var latencyContent =
            $"> __API Latency:__ `{apiLatency}ms`\n" +
            $"> __Database Latency:__ `{databaseLatency}`";

        var section = new SectionBuilder()
            .WithAccessory(thumbnail)
            .AddComponents(
                new TextDisplayBuilder("## Ping"),
                new TextDisplayBuilder(latencyContent));

        var container = new ContainerBuilder()
            .WithAccentColor(ComponentTheme.AccentColor)
            .AddComponents(
                section,
                new SeparatorBuilder(
                    isDivider: true,
                    spacing: SeparatorSpacingSize.Small),
                new TextDisplayBuilder(ComponentFooter.Text));

        return new ComponentBuilderV2()
            .AddComponent(container)
            .Build();
    }
}
