using Discord;
using LastRide.Models;

namespace LastRide.Builders;

public sealed class AutoModComponentBuilder
{
    private const string EnabledEmoji = "<:Enabled:1541831759191212154>";
    private const string DisabledEmoji = "<:disabled:1541831823406014699>";
    private const int MaxContentPreview = 200;
    private const int MaxBadWordsPreview = 1500;

    private static readonly Color AccentColor = new(8, 4, 4);

    public MessageComponent BuildOverview(
        AutoModConfig config,
        string prefix,
        bool isPersistent,
        string? botAvatarUrl,
        string botName)
    {
        var statusEmoji = config.Enabled ? EnabledEmoji : DisabledEmoji;
        var statusText = config.Enabled ? "Enabled" : "Disabled";

        var rules = new List<string>();

        foreach (var rule in Enum.GetValues<AutoModRuleType>())
        {
            var ruleEnabled = config.IsRuleEnabled(rule);
            var emoji = ruleEnabled ? EnabledEmoji : DisabledEmoji;
            var action = config.GetAction(rule).ToDisplay();

            rules.Add($"{emoji} **{rule.DisplayName()}** — `{action}`");
        }

        var bypass = config.BypassRoleIds.Count == 0
            ? "`None`"
            : string.Join(", ", config.BypassRoleIds.Select(id => $"<@&{id}>"));

        var logChannel = config.LogChannelId is { } logId
            ? $"<#{logId}>"
            : "`Not set`";

        var body =
            $"> {statusEmoji} **Master Status:** {statusText}\n\n" +
            "### Rules\n" +
            string.Join("\n", rules) + "\n\n" +
            "### Settings\n" +
            $"> **Bypass Roles:** {bypass}\n" +
            $"> **Log Channel:** {logChannel}\n" +
            $"> **Bad Words:** `{config.BadWords.Count}` configured\n\n" +
            $"-# Toggle a rule with `{prefix}<rule> on/off` • set action with `{prefix}<rule> action <delete|warn|mute|kick|ban>`";

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader("AutoMod Overview", body, botAvatarUrl, botName)
        };

        AppendPersistenceNote(components, isPersistent);
        AppendFooter(components);

        return BuildContainer(components.ToArray());
    }

    public MessageComponent BuildMasterToggled(
        bool enabled,
        bool isPersistent,
        string prefix)
    {
        var emoji = enabled ? EnabledEmoji : DisabledEmoji;
        var state = enabled ? "enabled" : "disabled";

        var lines = new List<string>
        {
            $"> {emoji} AutoMod is now **{state}** for this server."
        };

        if (enabled)
        {
            lines.Add(
                $"> Turn on individual rules with `{prefix}<rule> on` (e.g. `{prefix}antilink on`).");
        }

        return BuildStatusCard(
            enabled ? "AutoMod Enabled" : "AutoMod Disabled",
            lines,
            isPersistent);
    }

    public MessageComponent BuildRulesConfigurator(
        string title,
        string subtitle,
        AutoModConfig config,
        ulong requesterId,
        ulong guildId,
        bool isPersistent,
        string prefix)
    {
        var total = Enum.GetValues<AutoModRuleType>().Length;
        var enabledCount = Enum.GetValues<AutoModRuleType>()
            .Count(config.IsRuleEnabled);

        var header =
            $"## {EscapeMarkdown(title)}\n" +
            $"> {subtitle}\n" +
            $"> **Rules on:** `{enabledCount}/{total}`\n\n" +
            $"-# Selected rules turn on • unselected turn off • fine-tune with `{prefix}<rule> on/off`";

        var components = new List<IMessageComponentBuilder>
        {
            new TextDisplayBuilder(header),
            new SeparatorBuilder(
                isDivider: false,
                spacing: SeparatorSpacingSize.Small),
            BuildRulesMenu(config, requesterId, guildId)
        };

        AppendPersistenceNote(components, isPersistent);
        AppendFooter(components);

        return BuildContainer(components.ToArray());
    }

    private static ActionRowBuilder BuildRulesMenu(
        AutoModConfig config,
        ulong requesterId,
        ulong guildId)
    {
        var menu = new SelectMenuBuilder()
            .WithCustomId(AutoModComponentIds.CreateRulesMenu(requesterId, guildId))
            .WithPlaceholder("Select rules to enable")
            .WithMinValues(0)
            .WithMaxValues(Enum.GetValues<AutoModRuleType>().Length);

        foreach (var rule in Enum.GetValues<AutoModRuleType>())
        {
            menu.AddOption(
                rule.DisplayName(),
                rule.ToString(),
                MenuHint(rule),
                isDefault: config.IsRuleEnabled(rule));
        }

        return new ActionRowBuilder().WithSelectMenu(menu);
    }

    private static string MenuHint(AutoModRuleType rule)
    {
        return rule switch
        {
            AutoModRuleType.AntiCaps => "Blocks mostly-uppercase messages",
            AutoModRuleType.AntiDuplicate => "Blocks repeated messages",
            AutoModRuleType.AntiEmoji => "Blocks emoji spam",
            AutoModRuleType.AntiInvite => "Removes Discord invite links",
            AutoModRuleType.AntiLink => "Removes links (link-perm users exempt)",
            AutoModRuleType.AntiMention => "Blocks mass mentions",
            AutoModRuleType.AntiSpam => "Blocks fast message spam",
            AutoModRuleType.Badwords => "Blocks configured bad words",
            _ => string.Empty
        };
    }

    public MessageComponent BuildRuleStatus(
        AutoModRuleType rule,
        bool enabled,
        AutoModAction action,
        bool isPersistent,
        string prefix)
    {
        var emoji = enabled ? EnabledEmoji : DisabledEmoji;
        var command = rule.CommandName();

        var lines = new List<string>
        {
            $"> {emoji} **Status:** {(enabled ? "Enabled" : "Disabled")}",
            $"> **Action:** `{action.ToDisplay()}`",
            $"> {Describe(rule)}",
            $"> **Usage:** `{prefix}{command} on/off` • `{prefix}{command} action <delete|warn|mute|kick|ban>`"
        };

        return BuildStatusCard($"{emoji} {rule.DisplayName()}", lines, isPersistent);
    }

    public MessageComponent BuildBypassList(
        IReadOnlyCollection<ulong> roleIds,
        string? note,
        bool isPersistent,
        string prefix)
    {
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(note))
            lines.Add($"> {note}");

        if (roleIds.Count == 0)
        {
            lines.Add("> No bypass roles configured.");
        }
        else
        {
            lines.Add(
                $"> Members with any of these roles are exempt from **all** AutoMod rules.");
            lines.Add(string.Join("\n", roleIds.Select(id => $"> <@&{id}>")));
        }

        lines.Add(
            $"-# `{prefix}automodbypass add/remove @role` • `{prefix}automodbypass list`");

        return BuildStatusCard(
            $"AutoMod Bypass Roles ({roleIds.Count}/{AutoModDefaults.MaxBypassRoles})",
            lines,
            isPersistent);
    }

    public MessageComponent BuildBadWords(
        bool enabled,
        AutoModAction action,
        IReadOnlyCollection<string> words,
        string? note,
        bool isPersistent,
        string prefix)
    {
        var emoji = enabled ? EnabledEmoji : DisabledEmoji;

        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(note))
            lines.Add($"> {note}");

        lines.Add($"> {emoji} **Status:** {(enabled ? "Enabled" : "Disabled")}");
        lines.Add($"> **Action:** `{action.ToDisplay()}`");
        lines.Add($"> **Words:** `{words.Count}/{AutoModDefaults.MaxBadWords}`");

        if (words.Count == 0)
        {
            lines.Add("> No bad words configured yet.");
        }
        else
        {
            var preview = string.Join(
                ", ",
                words.Select(word => $"`{EscapeInlineCode(word)}`"));

            lines.Add($"> {Truncate(preview, MaxBadWordsPreview)}");
        }

        lines.Add(
            $"-# `{prefix}badwords add/remove <word>` • `{prefix}badwords on/off` • `{prefix}badwords action <delete|warn|mute|kick|ban>`");

        return BuildStatusCard($"{emoji} Bad Words", lines, isPersistent);
    }

    public MessageComponent BuildLogChannel(
        ulong? channelId,
        string? note,
        bool isPersistent,
        string prefix)
    {
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(note))
            lines.Add($"> {note}");

        lines.Add(channelId is { } id
            ? $"> **Log Channel:** <#{id}>"
            : "> **Log Channel:** `Not set`");

        lines.Add(
            $"-# `{prefix}automodlog #channel` to set • `{prefix}automodlog disable` to clear");

        return BuildStatusCard("AutoMod Log Channel", lines, isPersistent);
    }

    public MessageComponent BuildViolationNotice(
        ulong userId,
        AutoModRuleType rule,
        AutoModAction action)
    {
        var content =
            $"## Message Removed\n" +
            $"> <@{userId}>, your message broke the **{rule.DisplayName()}** rule.\n" +
            $"> **Action:** `{action.ToDisplay()}`";

        return BuildContainer(new TextDisplayBuilder(content));
    }

    public MessageComponent BuildLog(
        ulong userId,
        string userName,
        string? avatarUrl,
        AutoModRuleType rule,
        AutoModAction action,
        ulong channelId,
        string content)
    {
        var preview = string.IsNullOrWhiteSpace(content)
            ? "*No text content.*"
            : $"`{EscapeInlineCode(Truncate(content, MaxContentPreview))}`";

        var body =
            $"> **User:** <@{userId}> (`{EscapeInlineCode(userName)}`)\n" +
            $"> **Rule:** {rule.DisplayName()}\n" +
            $"> **Action:** `{action.ToDisplay()}`\n" +
            $"> **Channel:** <#{channelId}>\n" +
            $"> **Content:** {preview}";

        var components = new List<IMessageComponentBuilder>
        {
            BuildHeader("AutoMod Action", body, avatarUrl, userName)
        };

        AppendFooter(components);

        return BuildContainer(components.ToArray());
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

    private static string Describe(AutoModRuleType rule)
    {
        return rule switch
        {
            AutoModRuleType.AntiCaps =>
                $"Deletes messages that are mostly uppercase (≥{(int)(AutoModDefaults.CapsRatio * 100)}% caps, min {AutoModDefaults.CapsMinLength} letters).",
            AutoModRuleType.AntiDuplicate =>
                $"Blocks the same message repeated {AutoModDefaults.DuplicateCount} times in a row.",
            AutoModRuleType.AntiEmoji =>
                $"Blocks messages with more than {AutoModDefaults.MaxEmojis} emojis.",
            AutoModRuleType.AntiInvite =>
                "Removes Discord server invite links.",
            AutoModRuleType.AntiLink =>
                "Removes links. Members with Embed Links / Attach Files in a channel are exempt there.",
            AutoModRuleType.AntiMention =>
                $"Blocks messages mentioning more than {AutoModDefaults.MaxMentions} users/roles.",
            AutoModRuleType.AntiSpam =>
                $"Blocks sending {AutoModDefaults.SpamMessages}+ messages within {AutoModDefaults.SpamWindowSeconds}s.",
            AutoModRuleType.Badwords =>
                "Deletes messages containing any word on the bad-words list.",
            _ => string.Empty
        };
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

    private static IMessageComponentBuilder BuildHeader(
        string title,
        string content,
        string? avatarUrl,
        string avatarDescription)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
        {
            return new TextDisplayBuilder(
                $"## {EscapeMarkdown(title)}\n{content}");
        }

        return new SectionBuilder()
            .WithAccessory(
                new ThumbnailBuilder(
                    new UnfurledMediaItemProperties(avatarUrl),
                    description: avatarDescription))
            .AddComponents(
                new TextDisplayBuilder($"## {EscapeMarkdown(title)}"),
                new TextDisplayBuilder(content));
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
