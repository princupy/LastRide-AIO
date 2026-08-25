using System.Runtime.InteropServices;
using Discord;
using LastRide.Models;

namespace LastRide.Builders;

public sealed class StatsComponentBuilder
{
    private static readonly Color AccentColor = new(8, 4, 4);

    public MessageComponent BuildGeneral(
        BotStatsSnapshot stats,
        ulong userId)
    {
        var generalInfo =
            "### General Info\n" +
            "> **Bot:** `LastRide`\n" +
            "> **Status:** `Online`\n" +
            $"> **Connection:** `{stats.ConnectionState}`\n" +
            $"> **MongoDB:** `{stats.DatabaseStatus}`\n" +
            $"> **Gateway:** `{stats.GatewayLatency} ms`\n" +
            $"> **Uptime:** `{FormatUptime(stats.Uptime)}`";

        var serverStats =
            "### Server Stats\n" +
            $"> **Servers:** `{stats.GuildCount:N0}`\n" +
            $"> **Users:** `{stats.UserCount:N0}`\n" +
            $"> **Channels:** `{stats.ChannelCount:N0}`";

        var processStats =
            "### Process Stats\n" +
            $"> **Memory:** `{stats.MemoryMegabytes:N1} MB`\n" +
            $"> **Runtime:** `{stats.DotNetVersion}`";

        return BuildContainer(
            new TextDisplayBuilder("## LastRide Statistics"),
            Divider(),
            new TextDisplayBuilder(generalInfo),
            Divider(),
            new TextDisplayBuilder(serverStats),
            Divider(),
            new TextDisplayBuilder(processStats),
            Divider(isDivider: false),
            BuildButtons(StatsPanelTab.General, userId),
            FooterSeparator(),
            new TextDisplayBuilder(ComponentFooter.Text));
    }

    public MessageComponent BuildDeveloper(
        BotStatsSnapshot stats,
        ulong userId)
    {
        var developerInfo =
            "### Build Profile\n" +
            "> **Core:** `C# / .NET 8`\n" +
            $"> **Runtime:** `{stats.DotNetVersion}`\n" +
            $"> **Discord SDK:** `Discord.Net {stats.DiscordNetVersion}`\n" +
            "> **Commands:** `Prefix command handler`\n" +
            "> **Interface:** `Discord Components V2`\n" +
            "> **Storage:** `MongoDB Atlas`\n" +
            $"> **Host OS:** `{EscapeInlineCode(RuntimeInformation.OSDescription)}`\n" +
            $"> **Process:** `{RuntimeInformation.ProcessArchitecture}`";

        return BuildContainer(
            new TextDisplayBuilder("## LastRide Development"),
            Divider(),
            new TextDisplayBuilder(developerInfo),
            Divider(isDivider: false),
            BuildButtons(StatsPanelTab.Developer, userId),
            FooterSeparator(),
            new TextDisplayBuilder(ComponentFooter.Text));
    }

    public MessageComponent BuildTeam(
        IUser? owner,
        ulong userId)
    {
        var ownerText = owner is null
            ? "### Team\n" +
              "> **Owner:** `Unable to fetch owner`\n" +
              "> **Status:** `Set LASTRIDE_OWNER_ID or check bot application ownership`\n" +
              "> <:insta:1541369705632366603> [tanmoy_here8388](https://www.instagram.com/tanmoy_here8388)"
            : BuildOwnerText(owner);

        var container = new ContainerBuilder()
            .WithAccentColor(AccentColor);

        if (owner is null)
        {
            container.AddComponents(
                new TextDisplayBuilder("## LastRide Team"),
                Divider(),
                new TextDisplayBuilder(ownerText));
        }
        else
        {
            var displayName = GetDisplayName(owner);
            var avatarUrl = owner.GetDisplayAvatarUrl(size: 256);
            var thumbnail = new ThumbnailBuilder(
                new UnfurledMediaItemProperties(avatarUrl),
                description: displayName);

            var section = new SectionBuilder()
                .WithAccessory(thumbnail)
                .AddComponents(
                    new TextDisplayBuilder("## LastRide Team"),
                    new TextDisplayBuilder(ownerText));

            container.AddComponent(section);
        }

        container.AddComponents(
            Divider(isDivider: false),
            BuildButtons(StatsPanelTab.Team, userId),
            FooterSeparator(),
            new TextDisplayBuilder(ComponentFooter.Text));

        return new ComponentBuilderV2()
            .AddComponent(container)
            .Build();
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

    private static ActionRowBuilder BuildButtons(
        StatsPanelTab selectedTab,
        ulong userId)
    {
        return new ActionRowBuilder()
            .WithButton(
                ButtonBuilder
                    .CreateSecondaryButton(
                        "General",
                        StatsComponentIds.Create(StatsPanelTab.General, userId))
                    .WithDisabled(selectedTab == StatsPanelTab.General))
            .WithButton(
                ButtonBuilder
                    .CreateSecondaryButton(
                        "Development",
                        StatsComponentIds.Create(StatsPanelTab.Developer, userId))
                    .WithDisabled(selectedTab == StatsPanelTab.Developer))
            .WithButton(
                ButtonBuilder
                    .CreateSecondaryButton(
                        "Team",
                        StatsComponentIds.Create(StatsPanelTab.Team, userId))
                    .WithDisabled(selectedTab == StatsPanelTab.Team));
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

    private static string BuildOwnerText(IUser owner)
    {
        var username = owner.DiscriminatorValue == 0
            ? owner.Username
            : $"{owner.Username}#{owner.Discriminator}";

        return
            "### Team\n" +
            $"> **Owner:** {owner.Mention}\n" +
            $"> **Name:** `{EscapeInlineCode(GetDisplayName(owner))}`\n" +
            $"> **Username:** `{EscapeInlineCode(username)}`\n" +
            $"> **User ID:** `{owner.Id}`\n" +
            $"> **Discord Profile:** [Open Profile](https://discord.com/users/{owner.Id})\n" +
            "> <:insta:1541369705632366603> [tanmoy_here8388](https://www.instagram.com/tanmoy_here8388)";
    }

    private static string GetDisplayName(IUser owner)
    {
        return string.IsNullOrWhiteSpace(owner.GlobalName)
            ? owner.Username
            : owner.GlobalName;
    }

    private static string EscapeInlineCode(string value)
    {
        return value.Replace("`", "'");
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.Days > 0)
        {
            return
                $"{uptime.Days}d " +
                $"{uptime.Hours}h " +
                $"{uptime.Minutes}m " +
                $"{uptime.Seconds}s";
        }

        if (uptime.Hours > 0)
        {
            return
                $"{uptime.Hours}h " +
                $"{uptime.Minutes}m " +
                $"{uptime.Seconds}s";
        }

        return $"{uptime.Minutes}m {uptime.Seconds}s";
    }
}
