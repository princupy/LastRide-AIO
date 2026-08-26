using Discord;

namespace LastRide.Builders;

public sealed class HelpComponentBuilder
{
    private const int TotalCommandCount = 67;
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
            $"> <:ArrowRight:1541407020257640470> **Server Prefix:** `{prefix}`\n" +
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
        var components = new List<IMessageComponentBuilder>();

        if (selectedCategory == HelpCategory.AutoMod)
        {
            // AutoMod page shows two sections split by a separator: the core
            // AutoMod commands, then the Badwords sub-feature below it.
            components.Add(new TextDisplayBuilder(BuildAutoModContent(prefix)));
            components.Add(Divider());
            components.Add(new TextDisplayBuilder(BuildBadwordsContent(prefix)));
        }
        else if (selectedCategory == HelpCategory.AutoRole)
        {
            // AutoRole page stacks three sections split by separators: the core
            // Autorole commands, then Autoresponder, then the VC Role feature.
            components.Add(new TextDisplayBuilder(BuildAutoRoleContent(prefix)));
            components.Add(Divider());
            components.Add(new TextDisplayBuilder(BuildAutoResponderContent(prefix)));
            components.Add(Divider());
            components.Add(new TextDisplayBuilder(BuildVcRoleContent(prefix)));
        }
        else if (selectedCategory == HelpCategory.Voice)
        {
            // Voice page stacks three sections split by separators: mute &
            // deafen, then move/kick/pull, then channel controls & info.
            components.Add(new TextDisplayBuilder(BuildVoiceStateContent(prefix)));
            components.Add(Divider());
            components.Add(new TextDisplayBuilder(BuildVoiceMoveContent(prefix)));
            components.Add(Divider());
            components.Add(new TextDisplayBuilder(BuildVoiceChannelContent(prefix)));
        }
        else
        {
            components.Add(new TextDisplayBuilder(BuildCategoryContent(
                selectedCategory,
                prefix)));
        }

        components.Add(Divider());
        components.Add(BuildCategoryMenu(userId, selectedCategory));
        components.Add(FooterSeparator());
        components.Add(new TextDisplayBuilder(ComponentFooter.Text));

        var container = new ContainerBuilder()
            .WithAccentColor(AccentColor)
            .AddComponents(components.ToArray());

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
            HelpCategory.AutoMod => BuildAutoModContent(prefix),
            HelpCategory.AutoRole => BuildAutoRoleContent(prefix),
            HelpCategory.Voice => BuildVoiceStateContent(prefix),
            HelpCategory.Utility => BuildUtilityContent(prefix),
            HelpCategory.Moderation => BuildModerationContent(prefix),
            _ => BuildUtilityContent(prefix)
        };
    }

    private static string BuildAutoModContent(string prefix)
    {
        return
            "## AutoMod commands\n\n" +
            $"`{prefix}automod`, `{prefix}anticaps`, `{prefix}antiduplicate`, `{prefix}antiemoji`, `{prefix}antiinvite`, `{prefix}antilink`, `{prefix}antimention`, `{prefix}antispam`, `{prefix}automodbypass`, `{prefix}automodlog`\n\n" +
            $"-# `{prefix}automod` for the full overview • `{prefix}<rule> on/off` • `{prefix}<rule> action <delete|warn|mute|kick|ban>`";
    }

    private static string BuildBadwordsContent(string prefix)
    {
        return
            "## Badwords\n\n" +
            $"`{prefix}badwords add`, `{prefix}badwords remove`, `{prefix}badwords list`, `{prefix}badwords on`, `{prefix}badwords off`, `{prefix}badwords action`\n\n" +
            $"-# `{prefix}badwords add <word>` to blacklist a word • `{prefix}badwords action <delete|warn|mute|kick|ban>`";
    }

    private static string BuildAutoRoleContent(string prefix)
    {
        return
            "## Autorole commands\n\n" +
            $"`{prefix}autorole add`, `{prefix}autorole humans`, `{prefix}autorole bots`, `{prefix}autorole remove`, `{prefix}autorole list`, `{prefix}autorole status`, `{prefix}autorole on`, `{prefix}autorole off`, `{prefix}autorole reset`\n\n" +
            $"-# Assigns roles to members and bots on join • `{prefix}autorole add @role` for everyone • `{prefix}autorole humans/bots @role` to target one";
    }

    private static string BuildVcRoleContent(string prefix)
    {
        return
            "## VC Role\n\n" +
            $"`{prefix}vcrole set`, `{prefix}vcrole remove`, `{prefix}vcrole status`, `{prefix}vcrole on`, `{prefix}vcrole off`, `{prefix}vcrole reset`\n\n" +
            $"-# Gives a role while in a voice channel, auto-removed on leave • `{prefix}vcrole set @role`";
    }

    private static string BuildVoiceStateContent(string prefix)
    {
        return
            "## Voice — Mute & Deafen\n\n" +
            $"`{prefix}vcmute`, `{prefix}vcunmute`, `{prefix}vcmuteall`, `{prefix}vcunmuteall`, `{prefix}vcdeafen`, `{prefix}vcundeafen`, `{prefix}vcdeafenall`, `{prefix}vcundeafenall`";
    }

    private static string BuildVoiceMoveContent(string prefix)
    {
        return
            "## Voice — Move, Kick & Pull\n\n" +
            $"`{prefix}vcmove`, `{prefix}vcmoveall`, `{prefix}vckick`, `{prefix}vckickall`, `{prefix}vcpull`, `{prefix}vcpullall`";
    }

    private static string BuildVoiceChannelContent(string prefix)
    {
        return
            "## Voice — Channel & Info\n\n" +
            $"`{prefix}vclock`, `{prefix}vcunlock`, `{prefix}vchide`, `{prefix}vcunhide`, `{prefix}vclist`";
    }

    private static string BuildAutoResponderContent(string prefix)
    {
        return
            "## Autoresponder\n\n" +
            $"`{prefix}autoresponder add`, `{prefix}autoresponder edit`, `{prefix}autoresponder remove`, `{prefix}autoresponder list`\n\n" +
            $"-# Set up automatic replies to trigger words and phrases • `{prefix}autoresponder add <trigger> <reply>`";
    }

    private static string BuildUtilityContent(string prefix)
    {
        return
            "## Utility commands\n\n" +
            $"`{prefix}ping`, `{prefix}stats`, `{prefix}avatar`, `{prefix}banner`, `{prefix}afk`, `{prefix}snipe`, `{prefix}membercount`, `{prefix}userinfo`, `{prefix}serverinfo`, `{prefix}roleinfo`, `{prefix}help`";
    }

    private static string BuildModerationContent(string prefix)
    {
        return
            "## Moderation commands\n\n" +
            $"`{prefix}ban`, `{prefix}unban`, `{prefix}banlist`, `{prefix}kick`, `{prefix}mute`, `{prefix}unmute`, `{prefix}warn`, `{prefix}nick`, `{prefix}addrole`, `{prefix}roleicon`, `{prefix}steal`, `{prefix}deleteemoji`, `{prefix}setprefix`, `{prefix}snipe`, `{prefix}nuke`, `{prefix}purge`, `{prefix}lock`, `{prefix}unlock`, `{prefix}lockall`, `{prefix}unlockall`, `{prefix}hide`, `{prefix}unhide`, `{prefix}hideall`, `{prefix}unhideall`";
    }

    private static ActionRowBuilder BuildCategoryMenu(
        ulong userId,
        HelpCategory? selectedCategory)
    {
        var homeValue = HelpComponentIds.ToValue(HelpCategory.Home);
        var autoModValue = HelpComponentIds.ToValue(HelpCategory.AutoMod);
        var autoRoleValue = HelpComponentIds.ToValue(HelpCategory.AutoRole);
        var voiceValue = HelpComponentIds.ToValue(HelpCategory.Voice);
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
                "AutoMod",
                autoModValue,
                "Automatic moderation rules",
                isDefault: selectedCategory == HelpCategory.AutoMod)
            .AddOption(
                "AutoRole",
                autoRoleValue,
                "Assign roles on join and in voice",
                isDefault: selectedCategory == HelpCategory.AutoRole)
            .AddOption(
                "Voice",
                voiceValue,
                "Voice channel moderation commands",
                isDefault: selectedCategory == HelpCategory.Voice)
            .AddOption(
                "Moderation",
                moderationValue,
                "Server moderation commands",
                isDefault: selectedCategory == HelpCategory.Moderation)
            .AddOption(
                "Utility",
                utilityValue,
                "Ping and statistics commands",
                isDefault: selectedCategory == HelpCategory.Utility);

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
