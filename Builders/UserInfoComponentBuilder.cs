using Discord;
using Discord.WebSocket;

namespace LastRide.Builders;

public sealed class UserInfoComponentBuilder
{
    private static readonly Color AccentColor = ComponentTheme.AccentColor;

    public MessageComponent Build(SocketUser user, SocketGuildUser? guildUser)
    {
        var displayName = guildUser?.DisplayName ?? user.GlobalName ?? user.Username;
        var avatarUrl = guildUser?.GetDisplayAvatarUrl(size: 256) ??
            user.GetDisplayAvatarUrl(size: 256);

        var section = new SectionBuilder()
            .WithAccessory(
                new ThumbnailBuilder(
                    new UnfurledMediaItemProperties(avatarUrl),
                    description: displayName))
            .AddComponents(
                new TextDisplayBuilder($"## {EscapeMarkdown(displayName)} User Info"),
                new TextDisplayBuilder(BuildIdentity(user, guildUser)));

        var container = new ContainerBuilder()
            .WithAccentColor(AccentColor)
            .AddComponents(
                section,
                Divider(),
                new TextDisplayBuilder(BuildAccount(user)),
                Divider(),
                new TextDisplayBuilder(BuildServer(guildUser)),
                Divider(),
                new TextDisplayBuilder(BuildPresence(user, guildUser)),
                Divider(),
                new TextDisplayBuilder(BuildAssets(user, guildUser)),
                FooterSeparator(),
                new TextDisplayBuilder(ComponentFooter.Text));

        return new ComponentBuilderV2()
            .AddComponent(container)
            .Build();
    }

    private static string BuildIdentity(SocketUser user, SocketGuildUser? guildUser)
    {
        var tag = user.DiscriminatorValue == 0
            ? user.Username
            : $"{user.Username}#{user.Discriminator}";

        return
            "### Identity\n" +
            $"> **Mention:** {user.Mention}\n" +
            $"> **Username:** `{EscapeInlineCode(tag)}`\n" +
            $"> **Display Name:** `{EscapeInlineCode(user.GlobalName ?? "Not set")}`\n" +
            $"> **Server Name:** `{EscapeInlineCode(guildUser?.DisplayName ?? "Not in this server")}`\n" +
            $"> **User ID:** `{user.Id}`\n" +
            $"> **Account Type:** `{(user.IsBot ? "Bot" : user.IsWebhook ? "Webhook" : "Human")}`";
    }

    private static string BuildAccount(SocketUser user)
    {
        var publicFlags = user.PublicFlags?.ToString() ?? "None";

        return
            "### Account\n" +
            $"> **Created:** {FormatTimestamp(user.CreatedAt)}\n" +
            $"> **Created Ago:** {FormatRelative(user.CreatedAt)}\n" +
            $"> **Public Flags:** `{EscapeInlineCode(publicFlags)}`";
    }

    private static string BuildServer(SocketGuildUser? guildUser)
    {
        if (guildUser is null)
        {
            return
                "### Server\n" +
                "> This user is not cached as a member of this server.";
        }

        var roles = guildUser.Roles
            .Where(role => !role.IsEveryone)
            .OrderByDescending(role => role.Position)
            .ToArray();
        var topRole = roles.FirstOrDefault();
        var roleList = FormatRoleList(roles);

        return
            "### Server\n" +
            $"> **Joined:** {FormatOptionalTimestamp(guildUser.JoinedAt)}\n" +
            $"> **Joined Ago:** {FormatOptionalRelative(guildUser.JoinedAt)}\n" +
            $"> **Boosting Since:** {FormatOptionalTimestamp(guildUser.PremiumSince)}\n" +
            $"> **Top Role:** `{EscapeInlineCode(topRole?.Name ?? "None")}`\n" +
            $"> **Role Count:** `{roles.Length:N0}`\n" +
            $"> **Roles:** {roleList}\n" +
            $"> **Hierarchy:** `{guildUser.Hierarchy:N0}`";
    }

    private static string BuildPresence(SocketUser user, SocketGuildUser? guildUser)
    {
        var status = guildUser?.Status ?? user.Status;
        var activities = (guildUser?.Activities ?? user.Activities)
            .Select(FormatActivity)
            .Where(activity => !string.IsNullOrWhiteSpace(activity))
            .Take(3)
            .ToArray();

        var activityText = activities.Length == 0
            ? "`None`"
            : string.Join(", ", activities.Select(activity => $"`{EscapeInlineCode(activity)}`"));

        return
            "### Presence\n" +
            $"> **Status:** `{FormatStatus(status)}`\n" +
            $"> **Activities:** {activityText}";
    }

    private static string BuildAssets(SocketUser user, SocketGuildUser? guildUser)
    {
        var globalAvatar = user.GetAvatarUrl(size: 2048);
        var serverAvatar = guildUser?.GetGuildAvatarUrl(size: 2048);
        var serverBanner = guildUser?.GetGuildBannerUrl(size: 2048);
        var profileUrl = $"https://discord.com/users/{user.Id}";

        return
            "### Assets\n" +
            $"> **Global Avatar:** {FormatLink(globalAvatar)}\n" +
            $"> **Server Avatar:** {FormatLink(serverAvatar)}\n" +
            $"> **Server Banner:** {FormatLink(serverBanner)}\n" +
            $"> **Discord Profile:** [Open Profile]({profileUrl})";
    }

    private static string FormatRoleList(IReadOnlyList<SocketRole> roles)
    {
        if (roles.Count == 0)
            return "`None`";

        var names = roles
            .Take(10)
            .Select(role => $"`{EscapeInlineCode(role.Name)}`")
            .ToList();

        if (roles.Count > names.Count)
            names.Add($"`+{roles.Count - names.Count:N0} more`");

        return string.Join(", ", names);
    }

    private static string FormatActivity(IActivity activity)
    {
        var name = string.IsNullOrWhiteSpace(activity.Name)
            ? activity.Type.ToString()
            : activity.Name;

        return $"{FormatActivityType(activity.Type)} {name}";
    }

    private static string FormatStatus(UserStatus status)
    {
        return status switch
        {
            UserStatus.Online => "Online",
            UserStatus.Idle => "Idle",
            UserStatus.DoNotDisturb => "Do Not Disturb",
            UserStatus.Invisible => "Invisible",
            UserStatus.AFK => "AFK",
            UserStatus.Offline => "Offline",
            _ => status.ToString()
        };
    }

    private static string FormatActivityType(ActivityType type)
    {
        return type switch
        {
            ActivityType.Playing => "Playing",
            ActivityType.Streaming => "Streaming",
            ActivityType.Listening => "Listening to",
            ActivityType.Watching => "Watching",
            ActivityType.CustomStatus => "Custom",
            ActivityType.Competing => "Competing in",
            _ => type.ToString()
        };
    }

    private static string FormatLink(string? url)
    {
        return string.IsNullOrWhiteSpace(url)
            ? "`Not set`"
            : $"[Open Link]({url})";
    }

    private static string FormatTimestamp(DateTimeOffset timestamp)
    {
        return $"<t:{timestamp.ToUnixTimeSeconds()}:F>";
    }

    private static string FormatRelative(DateTimeOffset timestamp)
    {
        return $"<t:{timestamp.ToUnixTimeSeconds()}:R>";
    }

    private static string FormatOptionalTimestamp(DateTimeOffset? timestamp)
    {
        return timestamp.HasValue
            ? FormatTimestamp(timestamp.Value)
            : "`Not available`";
    }

    private static string FormatOptionalRelative(DateTimeOffset? timestamp)
    {
        return timestamp.HasValue
            ? FormatRelative(timestamp.Value)
            : "`Not available`";
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
