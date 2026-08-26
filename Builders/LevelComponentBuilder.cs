using Discord;
using Discord.WebSocket;
using LastRide.Models;

namespace LastRide.Builders;

/// <summary>
/// Renders every Leveling card — rank, leaderboard, config overview, level-up
/// announcement, and the small result/notice cards the commands reply with.
/// </summary>
public sealed class LevelComponentBuilder
{
    /// <summary>Leaderboard rows shown per page.</summary>
    public const int PageSize = 10;

    private static readonly Color AccentColor = new(8, 4, 4);

    private const string EnabledEmoji = "<:Enabled:1541831759191212154>";
    private const string DisabledEmoji = "<:disabled:1541831823406014699>";

    private const int ProgressSegments = 12;
    private const int MaxListedLevelRoles = 15;
    private const int MaxListedBlacklistEntries = 20;

    public MessageComponent BuildRankCard(
        SocketGuildUser member,
        long totalXp,
        int position,
        int total,
        bool isPersistent)
    {
        var level = LevelMath.LevelForXp(totalXp);
        var into = LevelMath.XpIntoCurrentLevel(totalXp);
        var span = LevelMath.XpSpanForLevel(level);

        var body =
            $"> **Level:** `{level}`\n" +
            $"> **Rank:** {FormatRank(position, total)}\n" +
            $"> **Total XP:** `{totalXp:N0}`";

        var progress =
            "### Progress\n" +
            $"> {BuildProgressBar(into, span)}\n" +
            $"> `{into:N0}` / `{span:N0}` XP to level `{level + 1}`";

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader(
                $"{member.DisplayName} — Text Rank",
                body,
                AvatarUrl(member),
                member.DisplayName),
            Divider(),
            new TextDisplayBuilder(progress)
        };

        AppendPersistenceNote(components, isPersistent);
        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    public MessageComponent BuildVoiceRankCard(
        SocketGuildUser member,
        long voiceXp,
        int position,
        int total,
        bool isPersistent)
    {
        var level = LevelMath.LevelForXp(voiceXp);
        var into = LevelMath.XpIntoCurrentLevel(voiceXp);
        var span = LevelMath.XpSpanForLevel(level);

        var body =
            $"> **Voice Level:** `{level}`\n" +
            $"> **Rank:** {FormatRank(position, total)}\n" +
            $"> **Total Voice XP:** `{voiceXp:N0}`\n" +
            $"> **Time In Voice:** `{FormatVoiceTime(voiceXp)}`";

        var progress =
            "### Progress\n" +
            $"> {BuildProgressBar(into, span)}\n" +
            $"> `{into:N0}` / `{span:N0}` XP to voice level `{level + 1}`";

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader(
                $"{member.DisplayName} — Voice Rank",
                body,
                AvatarUrl(member),
                member.DisplayName),
            Divider(),
            new TextDisplayBuilder(progress)
        };

        AppendPersistenceNote(components, isPersistent);
        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    public MessageComponent BuildLeaderboard(
        LevelBoard track,
        IReadOnlyList<LevelUser> ranked,
        SocketGuild guild,
        int page,
        ulong requesterId)
    {
        var isVoice = track == LevelBoard.Voice;
        var title = isVoice ? "Voice Leaderboard" : "Text Leaderboard";

        if (ranked.Count == 0)
        {
            return BuildContainer(
                BuildHeader(
                    title,
                    isVoice
                        ? "> Nobody has earned voice XP yet."
                        : "> Nobody has earned XP yet.",
                    guild.IconUrl,
                    guild.Name),
                FooterSeparator(),
                new TextDisplayBuilder(ComponentFooter.Text));
        }

        var totalPages = Math.Max(1, (ranked.Count + PageSize - 1) / PageSize);
        page = Math.Clamp(page, 0, totalPages - 1);

        var start = page * PageSize;
        var pageEntries = ranked.Skip(start).Take(PageSize).ToArray();

        var header =
            $"> Showing `{start + 1}`-`{start + pageEntries.Length}` of " +
            $"`{ranked.Count:N0}` member(s). Page `{page + 1}`/`{totalPages}`.";

        var rows = new List<string>();

        for (var index = 0; index < pageEntries.Length; index++)
        {
            var entry = pageEntries[index];
            var xp = isVoice ? entry.VoiceXp : entry.Xp;
            var level = LevelMath.LevelForXp(xp);

            rows.Add(
                $"> {FormatPlacement(start + index + 1)} <@{entry.UserId}> — " +
                $"Level `{level}` • `{xp:N0} XP`");
        }

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader(title, header, guild.IconUrl, guild.Name),
            Divider(),
            new TextDisplayBuilder(string.Join("\n", rows)),
            Divider(),
            BuildNavigationRow(track, page, totalPages, requesterId, guild.Id),
            FooterSeparator(),
            new TextDisplayBuilder(ComponentFooter.Text)
        };

        return BuildContainer(components.ToArray());
    }

    public MessageComponent BuildConfig(
        LevelConfig config,
        SocketGuild guild,
        string prefix,
        bool isPersistent)
    {
        var statusEmoji = config.Enabled ? EnabledEmoji : DisabledEmoji;
        var statusText = config.Enabled ? "Enabled" : "Disabled";

        var header =
            $"> {statusEmoji} **Master Status:** {statusText}\n" +
            $"> **XP Per Message:** `{config.MinXpPerMessage}` - `{config.MaxXpPerMessage}`\n" +
            $"> **XP Cooldown:** `{config.XpCooldownSeconds}s`\n" +
            $"> **Voice XP:** `{LevelDefaults.VoiceXpPerMinute}` per minute";

        var announceEmoji = config.LevelUpAnnouncementsEnabled
            ? EnabledEmoji
            : DisabledEmoji;

        var channelText = config.LevelUpChannelId is ulong channelId
            ? $"<#{channelId}>"
            : "`Where the level-up happens`";

        var announce =
            "### Level Up Announcements\n" +
            $"> {announceEmoji} **Status:** " +
            $"{(config.LevelUpAnnouncementsEnabled ? "Enabled" : "Disabled")}\n" +
            $"> **Channel:** {channelText}\n" +
            $"> **Message:** {FormatTemplate(config.LevelUpMessage)}";

        var rewards =
            "### Level Roles\n" +
            $"> **Mode:** `{config.RoleMode.DisplayName()}`\n" +
            FormatLevelRoles(config);

        var blacklist =
            "### Blacklists\n" +
            $"> **Channels:** `{config.BlacklistedChannelIds.Count}` blocked\n" +
            $"> **Roles:** `{config.BlacklistedRoleIds.Count}` blocked";

        var hint =
            $"-# `{prefix}setxprate <min> <max>` • `{prefix}setcooldown <seconds>` • " +
            $"`{prefix}levelrole add @role <level>` • `{prefix}setrankchannel #channel`";

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader("Leveling Settings", header, guild.IconUrl, guild.Name),
            Divider(),
            new TextDisplayBuilder(announce),
            Divider(),
            new TextDisplayBuilder(rewards),
            Divider(),
            new TextDisplayBuilder(blacklist),
            Divider(),
            new TextDisplayBuilder(hint)
        };

        AppendPersistenceNote(components, isPersistent);
        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    public MessageComponent BuildLevelUp(
        SocketGuildUser member,
        int level,
        string message,
        ulong? awardedRoleId)
    {
        var body = $"> {message}";

        if (awardedRoleId is ulong roleId)
            body += $"\n> **Reward Unlocked:** <@&{roleId}>";

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader(
                $"Level {level} Reached",
                body,
                AvatarUrl(member),
                member.DisplayName)
        };

        AppendFooter(components);
        return BuildContainer(components.ToArray());
    }

    public MessageComponent BuildChannelBlacklist(
        LevelConfig config,
        string prefix,
        bool isPersistent)
    {
        var lines = new List<string>
        {
            config.BlacklistedChannelIds.Count == 0
                ? "> No channels are blacklisted — XP is earned everywhere."
                : $"> `{config.BlacklistedChannelIds.Count}`/" +
                  $"`{LevelDefaults.MaxBlacklistedChannels}` channel(s) blocked."
        };

        if (config.BlacklistedChannelIds.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("### Blacklisted Channels");
            lines.Add(FormatMentionList(
                config.BlacklistedChannelIds,
                id => $"<#{id}>"));
        }

        lines.Add(string.Empty);
        lines.Add(
            $"-# `{prefix}blacklistchannel add #channel` • " +
            $"`{prefix}blacklistchannel remove #channel`");

        return BuildStatusCard("Leveling Channel Blacklist", lines, isPersistent);
    }

    public MessageComponent BuildRoleBlacklist(
        LevelConfig config,
        string prefix,
        bool isPersistent)
    {
        var lines = new List<string>
        {
            config.BlacklistedRoleIds.Count == 0
                ? "> No roles are blacklisted — every member can earn XP."
                : $"> `{config.BlacklistedRoleIds.Count}`/" +
                  $"`{LevelDefaults.MaxBlacklistedRoles}` role(s) blocked."
        };

        if (config.BlacklistedRoleIds.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("### Blacklisted Roles");
            lines.Add(FormatMentionList(
                config.BlacklistedRoleIds,
                id => $"<@&{id}>"));
        }

        lines.Add(string.Empty);
        lines.Add(
            $"-# `{prefix}blacklistrole add @role` • " +
            $"`{prefix}blacklistrole remove @role`");

        return BuildStatusCard("Leveling Role Blacklist", lines, isPersistent);
    }

    public MessageComponent BuildLevelRoleList(
        LevelConfig config,
        string prefix,
        bool isPersistent)
    {
        var lines = new List<string>
        {
            $"> **Mode:** `{config.RoleMode.DisplayName()}` — " +
            (config.RoleMode == LevelRoleMode.Stack
                ? "every earned reward is kept."
                : "only the highest reward is kept."),
            string.Empty,
            "### Rewards",
            FormatLevelRoles(config),
            string.Empty,
            $"-# `{prefix}levelrole add @role <level>` • " +
            $"`{prefix}levelrole remove @role` • `{prefix}levelrole mode <stack|replace>`"
        };

        return BuildStatusCard("Leveling Role Rewards", lines, isPersistent);
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

    private MessageComponent BuildStatusCard(
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

    private static ActionRowBuilder BuildNavigationRow(
        LevelBoard track,
        int page,
        int totalPages,
        ulong requesterId,
        ulong guildId)
    {
        return new ActionRowBuilder()
            .WithButton(ButtonBuilder.CreateSecondaryButton(
                    "Previous",
                    LevelComponentIds.Create(track, page - 1, requesterId, guildId))
                .WithDisabled(page <= 0))
            .WithButton(ButtonBuilder.CreateSecondaryButton(
                    "Next",
                    LevelComponentIds.Create(track, page + 1, requesterId, guildId))
                .WithDisabled(page >= totalPages - 1));
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

    private static SeparatorBuilder Divider()
    {
        return new SeparatorBuilder(isDivider: true, spacing: SeparatorSpacingSize.Small);
    }

    private static SeparatorBuilder FooterSeparator()
    {
        return new SeparatorBuilder(isDivider: true, spacing: SeparatorSpacingSize.Small);
    }

    private static string FormatLevelRoles(LevelConfig config)
    {
        if (config.LevelRoles.Count == 0)
            return "> No level roles configured yet.";

        var ordered = config.LevelRoles.OrderBy(pair => pair.Key).ToArray();

        var lines = ordered
            .Take(MaxListedLevelRoles)
            .Select(pair => $"> **Level `{pair.Key}`** — <@&{pair.Value}>")
            .ToList();

        if (ordered.Length > MaxListedLevelRoles)
            lines.Add($"> …and `{ordered.Length - MaxListedLevelRoles}` more.");

        return string.Join("\n", lines);
    }

    private static string FormatMentionList(
        IEnumerable<ulong> ids,
        Func<ulong, string> format)
    {
        var all = ids.ToArray();

        var mentions = all
            .Take(MaxListedBlacklistEntries)
            .Select(format)
            .ToList();

        var rendered = $"> {string.Join(", ", mentions)}";

        if (all.Length > MaxListedBlacklistEntries)
            rendered += $"\n> …and `{all.Length - MaxListedBlacklistEntries}` more.";

        return rendered;
    }

    private static string FormatTemplate(string? template)
    {
        return string.IsNullOrWhiteSpace(template)
            ? "`Default`"
            : $"`{EscapeInlineCode(Truncate(template, 120))}`";
    }

    private static string FormatRank(int position, int total)
    {
        return position <= 0
            ? "`Unranked`"
            : $"`#{position}` of `{total:N0}`";
    }

    private static string FormatPlacement(int position)
    {
        return position switch
        {
            1 => "🥇",
            2 => "🥈",
            3 => "🥉",
            _ => $"`#{position}`"
        };
    }

    private static string FormatVoiceTime(long voiceXp)
    {
        if (voiceXp <= 0 || LevelDefaults.VoiceXpPerMinute <= 0)
            return "0m";

        var minutes = voiceXp / LevelDefaults.VoiceXpPerMinute;
        var hours = minutes / 60;
        var remainder = minutes % 60;

        return hours > 0 ? $"{hours}h {remainder}m" : $"{remainder}m";
    }

    private static string BuildProgressBar(long current, long max)
    {
        if (max <= 0)
            return new string('█', ProgressSegments) + " `100%`";

        var ratio = Math.Clamp((double)current / max, 0, 1);
        var filled = Math.Clamp((int)Math.Round(ratio * ProgressSegments), 0, ProgressSegments);

        return new string('█', filled) +
            new string('░', ProgressSegments - filled) +
            $" `{ratio * 100:0}%`";
    }

    private static string AvatarUrl(SocketGuildUser member)
    {
        return member.GetDisplayAvatarUrl(size: 256) ?? member.GetDefaultAvatarUrl();
    }

    private static string Truncate(string value, int maxLength)
    {
        var collapsed = value.Replace("\n", " ").Replace("\r", " ").Trim();

        return collapsed.Length <= maxLength
            ? collapsed
            : collapsed[..(maxLength - 1)] + "…";
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
