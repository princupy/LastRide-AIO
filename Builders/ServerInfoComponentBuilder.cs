using Discord;
using Discord.WebSocket;

namespace LastRide.Builders;

public sealed class ServerInfoComponentBuilder
{
    private static readonly Color AccentColor = new(8, 4, 4);

    public MessageComponent Build(
        SocketGuild guild,
        ulong requesterId,
        ServerInfoPage selectedPage)
    {
        var container = new ContainerBuilder()
            .WithAccentColor(AccentColor);
        var iconUrl = GetServerIconUrl(guild, 256);

        if (!string.IsNullOrWhiteSpace(iconUrl))
        {
            container.AddComponent(
                new SectionBuilder()
                    .WithAccessory(
                        new ThumbnailBuilder(
                            new UnfurledMediaItemProperties(iconUrl),
                            description: guild.Name))
                    .AddComponents(
                        new TextDisplayBuilder($"## {EscapeMarkdown(guild.Name)} Server Info"),
                        new TextDisplayBuilder(BuildPageContent(guild, selectedPage))));
        }
        else
        {
            container.AddComponents(
                new TextDisplayBuilder($"## {EscapeMarkdown(guild.Name)} Server Info"),
                new TextDisplayBuilder(BuildPageContent(guild, selectedPage)));
        }

        container.AddComponents(
            Divider(isDivider: false),
            BuildButtons(selectedPage, requesterId, guild.Id),
            FooterSeparator(),
            new TextDisplayBuilder(ComponentFooter.Text));

        return new ComponentBuilderV2()
            .AddComponent(container)
            .Build();
    }

    private static string BuildPageContent(
        SocketGuild guild,
        ServerInfoPage page)
    {
        return page switch
        {
            ServerInfoPage.Members => BuildMembers(guild),
            ServerInfoPage.Channels => BuildChannels(guild),
            ServerInfoPage.Roles => BuildRoles(guild),
            ServerInfoPage.Assets => BuildAssets(guild),
            _ => BuildOverview(guild)
        };
    }

    private static string BuildOverview(SocketGuild guild)
    {
        var ownerName = guild.Owner is null
            ? "Unknown"
            : GetUserName(guild.Owner);
        var description = string.IsNullOrWhiteSpace(guild.Description)
            ? "Not set"
            : Truncate(guild.Description, 180);

        return
            "### Overview\n" +
            $"> **Server Name:** `{EscapeInlineCode(guild.Name)}`\n" +
            $"> **Server ID:** `{guild.Id}`\n" +
            $"> **Owner:** `{EscapeInlineCode(ownerName)}` (`{guild.OwnerId}`)\n" +
            $"> **Created:** {FormatTimestamp(guild.CreatedAt)}\n" +
            $"> **Created Ago:** {FormatRelative(guild.CreatedAt)}\n" +
            $"> **Description:** `{EscapeInlineCode(description)}`\n" +
            $"> **Locale:** `{EscapeInlineCode(guild.PreferredLocale)}`";
    }

    private static string BuildMembers(SocketGuild guild)
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
        var boosters = users.Count(user => user.PremiumSince.HasValue);

        return
            "### Members\n" +
            $"> **Total Members:** `{guild.MemberCount:N0}`\n" +
            $"> **Cached Members:** `{cachedMembers:N0}`\n" +
            $"> **Humans:** `{humans:N0}`\n" +
            $"> **Bots:** `{bots:N0}`\n" +
            $"> **Online:** `{online:N0}`\n" +
            $"> **Idle:** `{idle:N0}`\n" +
            $"> **Do Not Disturb:** `{dnd:N0}`\n" +
            $"> **Offline:** `{offline:N0}`\n" +
            $"> **Boosters Cached:** `{boosters:N0}`\n" +
            $"> **Full Cache:** `{(guild.HasAllMembers ? "Yes" : "No")}`";
    }

    private static string BuildChannels(SocketGuild guild)
    {
        var total =
            guild.TextChannels.Count +
            guild.VoiceChannels.Count +
            guild.StageChannels.Count +
            guild.ForumChannels.Count +
            guild.MediaChannels.Count +
            guild.CategoryChannels.Count +
            guild.ThreadChannels.Count;

        return
            "### Channels\n" +
            $"> **Total:** `{total:N0}`\n" +
            $"> **Text:** `{guild.TextChannels.Count:N0}`\n" +
            $"> **Voice:** `{guild.VoiceChannels.Count:N0}`\n" +
            $"> **Stage:** `{guild.StageChannels.Count:N0}`\n" +
            $"> **Forum:** `{guild.ForumChannels.Count:N0}`\n" +
            $"> **Media:** `{guild.MediaChannels.Count:N0}`\n" +
            $"> **Categories:** `{guild.CategoryChannels.Count:N0}`\n" +
            $"> **Threads:** `{guild.ThreadChannels.Count:N0}`\n" +
            $"> **System Channel:** {FormatChannel(guild.SystemChannel)}\n" +
            $"> **Rules Channel:** {FormatChannel(guild.RulesChannel)}";
    }

    private static string BuildRoles(SocketGuild guild)
    {
        var roles = guild.Roles
            .Where(role => !role.IsEveryone)
            .OrderByDescending(role => role.Position)
            .ToArray();
        var managed = roles.Count(role => role.IsManaged);
        var mentionable = roles.Count(role => role.IsMentionable);
        var visibleRoles = roles
            .Take(12)
            .Select(role => $"`{EscapeInlineCode(role.Name)}`")
            .ToList();

        if (roles.Length > visibleRoles.Count)
            visibleRoles.Add($"`+{roles.Length - visibleRoles.Count:N0} more`");

        var topRoles = visibleRoles.Count == 0
            ? "`None`"
            : string.Join(", ", visibleRoles);

        return
            "### Roles\n" +
            $"> **Total Roles:** `{roles.Length:N0}`\n" +
            $"> **Managed Roles:** `{managed:N0}`\n" +
            $"> **Mentionable Roles:** `{mentionable:N0}`\n" +
            $"> **Everyone Role ID:** `{guild.EveryoneRole.Id}`\n" +
            $"> **Top Roles:** {topRoles}";
    }

    private static string BuildAssets(SocketGuild guild)
    {
        var features = FormatFeatures(guild.Features);
        var iconUrl = GetServerIconUrl(guild, 2048);

        return
            "### Assets & Settings\n" +
            $"> **Icon:** {FormatLink(iconUrl)}\n" +
            $"> **Banner:** {FormatLink(guild.BannerUrl)}\n" +
            $"> **Splash:** {FormatLink(guild.SplashUrl)}\n" +
            $"> **Emotes:** `{guild.Emotes.Count:N0}`\n" +
            $"> **Stickers:** `{guild.Stickers.Count:N0}`\n" +
            $"> **Boost Tier:** `{guild.PremiumTier}`\n" +
            $"> **Boost Count:** `{FormatNullableNumber(guild.PremiumSubscriptionCount)}`\n" +
            $"> **Verification:** `{guild.VerificationLevel}`\n" +
            $"> **MFA Level:** `{guild.MfaLevel}`\n" +
            $"> **Content Filter:** `{guild.ExplicitContentFilter}`\n" +
            $"> **Default Notifications:** `{guild.DefaultMessageNotifications}`\n" +
            $"> **NSFW Level:** `{guild.NsfwLevel}`\n" +
            $"> **AFK Channel:** {FormatVoiceChannel(guild.AFKChannel)}\n" +
            $"> **AFK Timeout:** `{FormatSeconds(guild.AFKTimeout)}`\n" +
            $"> **Upload Limit:** `{FormatBytes(guild.MaxUploadLimit)}`\n" +
            $"> **Max Bitrate:** `{guild.MaxBitrate / 1000:N0} kbps`\n" +
            $"> **Features:** {features}";
    }

    private static ActionRowBuilder BuildButtons(
        ServerInfoPage selectedPage,
        ulong requesterId,
        ulong guildId)
    {
        return new ActionRowBuilder()
            .WithButton(
                ButtonBuilder
                    .CreateSecondaryButton(
                        "Overview",
                        ServerInfoComponentIds.Create(
                            ServerInfoPage.Overview,
                            requesterId,
                            guildId))
                    .WithDisabled(selectedPage == ServerInfoPage.Overview))
            .WithButton(
                ButtonBuilder
                    .CreateSecondaryButton(
                        "Members",
                        ServerInfoComponentIds.Create(
                            ServerInfoPage.Members,
                            requesterId,
                            guildId))
                    .WithDisabled(selectedPage == ServerInfoPage.Members))
            .WithButton(
                ButtonBuilder
                    .CreateSecondaryButton(
                        "Channels",
                        ServerInfoComponentIds.Create(
                            ServerInfoPage.Channels,
                            requesterId,
                            guildId))
                    .WithDisabled(selectedPage == ServerInfoPage.Channels))
            .WithButton(
                ButtonBuilder
                    .CreateSecondaryButton(
                        "Roles",
                        ServerInfoComponentIds.Create(
                            ServerInfoPage.Roles,
                            requesterId,
                            guildId))
                    .WithDisabled(selectedPage == ServerInfoPage.Roles))
            .WithButton(
                ButtonBuilder
                    .CreateSecondaryButton(
                        "Assets",
                        ServerInfoComponentIds.Create(
                            ServerInfoPage.Assets,
                            requesterId,
                            guildId))
                    .WithDisabled(selectedPage == ServerInfoPage.Assets));
    }

    private static string FormatChannel(SocketTextChannel? channel)
    {
        return channel is null
            ? "`Not set`"
            : $"`#{EscapeInlineCode(channel.Name)}`";
    }

    private static string FormatVoiceChannel(SocketVoiceChannel? channel)
    {
        return channel is null
            ? "`Not set`"
            : $"`{EscapeInlineCode(channel.Name)}`";
    }

    private static string FormatFeatures(GuildFeatures features)
    {
        var values = new List<string>();

        if (features.IsVerified)
            values.Add("Verified");
        if (features.IsPartnered)
            values.Add("Partnered");
        if (features.HasVanityUrl)
            values.Add("Vanity URL");
        if (features.HasThreads)
            values.Add("Threads");
        if (features.HasPrivateThreads)
            values.Add("Private Threads");
        if (features.HasTextInVoice)
            values.Add("Text in Voice");
        if (features.HasRoleIcons)
            values.Add("Role Icons");
        if (features.HasEnhancedRoleColors)
            values.Add("Enhanced Role Colors");
        if (features.HasRoleSubscriptions)
            values.Add("Role Subscriptions");

        values.AddRange(features.Experimental.Take(4));

        if (!values.Any())
            return "`None`";

        return string.Join(
            ", ",
            values
                .Take(8)
                .Select(feature => $"`{EscapeInlineCode(feature)}`"));
    }

    private static string FormatLink(string? url)
    {
        return string.IsNullOrWhiteSpace(url)
            ? "`Not set`"
            : $"[Open Link]({url})";
    }

    private static string? GetServerIconUrl(SocketGuild guild, ushort size)
    {
        if (!string.IsNullOrWhiteSpace(guild.IconUrl))
            return guild.IconUrl;

        return string.IsNullOrWhiteSpace(guild.IconId)
            ? null
            : CDN.GetGuildIconUrl(
                guild.Id,
                guild.IconId,
                size,
                ImageFormat.Auto);
    }

    private static string FormatNullableNumber(int? value)
    {
        return value.HasValue
            ? value.Value.ToString("N0")
            : "0";
    }

    private static string FormatSeconds(int seconds)
    {
        var value = TimeSpan.FromSeconds(seconds);

        if (value.TotalHours >= 1)
            return $"{value.TotalHours:N0}h";

        return $"{value.TotalMinutes:N0}m";
    }

    private static string FormatBytes(ulong bytes)
    {
        const double mib = 1024d * 1024d;

        return $"{bytes / mib:N0} MB";
    }

    private static string GetUserName(IUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.GlobalName))
            return user.GlobalName;

        return user.DiscriminatorValue == 0
            ? user.Username
            : $"{user.Username}#{user.Discriminator}";
    }

    private static string FormatTimestamp(DateTimeOffset timestamp)
    {
        return $"<t:{timestamp.ToUnixTimeSeconds()}:F>";
    }

    private static string FormatRelative(DateTimeOffset timestamp)
    {
        return $"<t:{timestamp.ToUnixTimeSeconds()}:R>";
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..(maxLength - 3)] + "...";
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
