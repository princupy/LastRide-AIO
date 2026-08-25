using Discord;
using Discord.WebSocket;

namespace LastRide.Builders;

public sealed class MemberCountComponentBuilder
{
    private static readonly Color AccentColor = new(8, 4, 4);

    public MessageComponent Build(SocketGuild guild)
    {
        var users = guild.Users;
        var cachedMembers = users.Count;
        var humans = users.Count(user => !user.IsBot);
        var bots = users.Count(user => user.IsBot);
        var online = users.Count(user => user.Status == UserStatus.Online);
        var idle = users.Count(user => user.Status == UserStatus.Idle);
        var dnd = users.Count(user => user.Status == UserStatus.DoNotDisturb);
        var offline = users.Count(user =>
            user.Status == UserStatus.Offline ||
            user.Status == UserStatus.Invisible);

        var memberStats =
            "### Members\n" +
            $"> **Total:** `{guild.MemberCount:N0}`\n" +
            $"> **Humans:** `{humans:N0}`\n" +
            $"> **Bots:** `{bots:N0}`";

        var presenceStats =
            "### Presence\n" +
            $"> **Online:** `{online:N0}`\n" +
            $"> **Idle:** `{idle:N0}`\n" +
            $"> **Do Not Disturb:** `{dnd:N0}`\n" +
            $"> **Offline:** `{offline:N0}`";

        var cacheNote =
            cachedMembers >= guild.MemberCount
                ? $"`{cachedMembers:N0}` members are loaded in cache."
                : $"`{cachedMembers:N0}` of `{guild.MemberCount:N0}` members are loaded in cache.";

        var container = new ContainerBuilder()
            .WithAccentColor(AccentColor)
            .AddComponents(
                new TextDisplayBuilder($"## {guild.Name} Member Count"),
                Divider(),
                new TextDisplayBuilder(memberStats),
                Divider(),
                new TextDisplayBuilder(presenceStats),
                Divider(isDivider: false),
                new TextDisplayBuilder($"> **Cache:** {cacheNote}"),
                FooterSeparator(),
                new TextDisplayBuilder(ComponentFooter.Text));

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
}
