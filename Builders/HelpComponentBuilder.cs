using Discord;

namespace LastRide.Builders;

public sealed class HelpComponentBuilder
{
    private const int TotalCommandCount = 28;
    private static readonly Color AccentColor = new(8, 4, 4);

    public MessageComponent Build(
        ulong userId,
        string prefix,
        string requesterMention,
        string botName,
        string botAvatarUrl,
        HelpCategory? selectedCategory = null)
    {
        if (selectedCategory is not null)
        {
            if (selectedCategory == HelpCategory.Home)
            {
                selectedCategory = null;
            }
            else
            {
                return BuildCategoryPage(
                    userId,
                    prefix,
                    selectedCategory.Value);
            }
        }

        var intro =
            $"Hey {requesterMention}, I'm {EscapeInlineCode(botName)}, your Discord companion.";

        var summary =
            $"> <:ArrowRight:1541407020257640470> **Default Prefix:** `{prefix}`\n" +
            $"> <:ArrowRight:1541407020257640470> **Total Commands:** `{TotalCommandCount}`";

        var section = new SectionBuilder()
            .WithAccessory(
                new ThumbnailBuilder(
                    new UnfurledMediaItemProperties(botAvatarUrl),
                    description: botName))
            .AddComponents(
                new TextDisplayBuilder($"## Welcome to {botName}"),
                new TextDisplayBuilder(intro),
                new TextDisplayBuilder(summary));

        var container = new ContainerBuilder()
            .WithAccentColor(AccentColor)
            .AddComponents(
                section,
                Divider(isDivider: false),
                new TextDisplayBuilder(
                    "__Use the dropdown menu below to explore categories.__"),
                BuildCategoryMenu(userId, selectedCategory),
                FooterSeparator(),
                new TextDisplayBuilder(ComponentFooter.Text));

        return new ComponentBuilderV2()
            .AddComponent(container)
            .Build();
    }

    private static MessageComponent BuildCategoryPage(
        ulong userId,
        string prefix,
        HelpCategory selectedCategory)
    {
        var container = new ContainerBuilder()
            .WithAccentColor(AccentColor)
            .AddComponents(
                new TextDisplayBuilder(BuildCategoryContent(
                    selectedCategory,
                    prefix)),
                Divider(),
                BuildCategoryMenu(userId, selectedCategory),
                FooterSeparator(),
                new TextDisplayBuilder(ComponentFooter.Text));

        return new ComponentBuilderV2()
            .AddComponent(container)
            .Build();
    }

    private static string BuildCategoryContent(
        HelpCategory category,
        string prefix)
    {
        return category switch
        {
            HelpCategory.Utility => BuildUtilityContent(prefix),
            HelpCategory.Moderation => BuildModerationContent(prefix),
            _ => BuildUtilityContent(prefix)
        };
    }

    private static string BuildUtilityContent(string prefix)
    {
        return
            "## Utility commands\n\n" +
            $"`{prefix}ping`, `{prefix}stats`, `{prefix}avatar`, `{prefix}banner`, `{prefix}afk`, `{prefix}membercount`, `{prefix}userinfo`, `{prefix}serverinfo`, `{prefix}roleinfo`, `{prefix}help`";
    }

    private static string BuildModerationContent(string prefix)
    {
        return
            "## Moderation commands\n\n" +
            $"`{prefix}ban`, `{prefix}unban`, `{prefix}kick`, `{prefix}mute`, `{prefix}unmute`, `{prefix}nick`, `{prefix}addrole`, `{prefix}roleicon`, `{prefix}nuke`, `{prefix}purge`, `{prefix}lock`, `{prefix}unlock`, `{prefix}lockall`, `{prefix}unlockall`, `{prefix}hide`, `{prefix}unhide`, `{prefix}hideall`, `{prefix}unhideall`";
    }

    private static ActionRowBuilder BuildCategoryMenu(
        ulong userId,
        HelpCategory? selectedCategory)
    {
        var homeValue = HelpComponentIds.ToValue(HelpCategory.Home);
        var utilityValue = HelpComponentIds.ToValue(HelpCategory.Utility);
        var moderationValue = HelpComponentIds.ToValue(HelpCategory.Moderation);

        var menu = new SelectMenuBuilder()
            .WithCustomId(HelpComponentIds.Create(userId))
            .WithPlaceholder("Select category to view commands")
            .WithMinValues(1)
            .WithMaxValues(1)
            .AddOption(
                "Home",
                homeValue,
                "Return to the main help menu",
                isDefault: selectedCategory is null)
            .AddOption(
                "Utility",
                utilityValue,
                "Ping and statistics commands",
                isDefault: selectedCategory == HelpCategory.Utility)
            .AddOption(
                "Moderation",
                moderationValue,
                "Server moderation commands",
                isDefault: selectedCategory == HelpCategory.Moderation);

        return new ActionRowBuilder()
            .WithSelectMenu(menu);
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
}
