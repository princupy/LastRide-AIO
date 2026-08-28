using Discord;

namespace LastRide.Builders;

public sealed class HelpComponentBuilder
{
    /// <summary>
    /// Marker for commands that must not be listed or counted anywhere. Set through
    /// <c>[Remarks]</c> on the command itself.
    /// </summary>
    public const string HiddenCommandRemark = "hidden";

    private static readonly Color AccentColor = ComponentTheme.AccentColor;

    public MessageComponent Build(
        ulong userId,
        string prefix,
        string requesterMention,
        string botName,
        string botAvatarUrl,
        int totalCommands,
        int availableCommands,
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

        // The access figure is scanned per reader, so two members looking at the same
        // menu see two different numbers — the second one is the full catalogue.
        var summary =
            $"> <:ArrowRight:1541407020257640470> **Server Prefix:** `{prefix}`\n" +
            $"> <:ArrowRight:1541407020257640470> **Your Access:** " +
            $"`{availableCommands}` / `{totalCommands}` commands";

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
        else if (selectedCategory == HelpCategory.Leveling)
        {
            // Leveling page stacks three sections split by separators: ranks &
            // leaderboards, then XP management, then the configuration commands.
            components.Add(new TextDisplayBuilder(BuildLevelingRanksContent(prefix)));
            components.Add(Divider());
            components.Add(new TextDisplayBuilder(BuildLevelingManageContent(prefix)));
            components.Add(Divider());
            components.Add(new TextDisplayBuilder(BuildLevelingConfigContent(prefix)));
        }
        else if (selectedCategory == HelpCategory.SetupRoles)
        {
            // Setup-Roles page stacks two sections split by a separator: the
            // staff allowlist, then the custom role commands built on top of it.
            components.Add(new TextDisplayBuilder(BuildSetupRoleStaffContent(prefix)));
            components.Add(Divider());
            components.Add(new TextDisplayBuilder(BuildSetupRoleCommandsContent(prefix)));
        }
        else if (selectedCategory == HelpCategory.Welcome)
        {
            // Welcome page stacks two sections split by a separator: the setup
            // commands, then the message template and its placeholders.
            components.Add(new TextDisplayBuilder(BuildWelcomeSetupContent(prefix)));
            components.Add(Divider());
            components.Add(new TextDisplayBuilder(BuildWelcomeMessageContent(prefix)));
        }
        else if (selectedCategory == HelpCategory.Ticket)
        {
            // Ticket page stacks three sections split by separators: the setup
            // commands, then what members can do, then the staff-only actions.
            components.Add(new TextDisplayBuilder(BuildTicketSetupContent(prefix)));
            components.Add(Divider());
            components.Add(new TextDisplayBuilder(BuildTicketUseContent(prefix)));
            components.Add(Divider());
            components.Add(new TextDisplayBuilder(BuildTicketStaffContent(prefix)));
        }
        else if (selectedCategory == HelpCategory.Media)
        {
            // Media page stacks two sections split by a separator: the channel
            // commands, then what enforcement and forwarding actually do.
            components.Add(new TextDisplayBuilder(BuildMediaChannelsContent(prefix)));
            components.Add(Divider());
            components.Add(new TextDisplayBuilder(BuildMediaForwardContent(prefix)));
        }
        else if (selectedCategory == HelpCategory.Music)
        {
            // Music page splits by what the command touches: the playback controls
            // first, then everything that acts on the queue.
            components.Add(new TextDisplayBuilder(BuildMusicPlaybackContent(prefix)));
            components.Add(Divider());
            components.Add(new TextDisplayBuilder(BuildMusicQueueContent(prefix)));
        }
        else if (selectedCategory == HelpCategory.Giveaway)
        {
            // Giveaway page splits by audience: the commands that need Manage
            // Server, then the two anyone can run.
            components.Add(new TextDisplayBuilder(BuildGiveawayHostContent(prefix)));
            components.Add(Divider());
            components.Add(new TextDisplayBuilder(BuildGiveawayInfoContent(prefix)));
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
            HelpCategory.Leveling => BuildLevelingRanksContent(prefix),
            HelpCategory.SetupRoles => BuildSetupRoleStaffContent(prefix),
            HelpCategory.Welcome => BuildWelcomeSetupContent(prefix),
            HelpCategory.Ticket => BuildTicketSetupContent(prefix),
            HelpCategory.Media => BuildMediaChannelsContent(prefix),
            HelpCategory.Music => BuildMusicPlaybackContent(prefix),
            HelpCategory.Giveaway => BuildGiveawayHostContent(prefix),
            HelpCategory.Logs => BuildLogsContent(prefix),
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

    private static string BuildLevelingRanksContent(string prefix)
    {
        return
            "## Leveling — Ranks & Leaderboards\n\n" +
            $"`{prefix}rank`, `{prefix}level`, `{prefix}leaderboard`, `{prefix}vcrank`, `{prefix}vclb`";
    }

    private static string BuildLevelingManageContent(string prefix)
    {
        return
            "## Leveling — XP Management\n\n" +
            $"`{prefix}addxp`, `{prefix}removexp`, `{prefix}setlevel`, `{prefix}rankreset`, `{prefix}vcreset`, `{prefix}vcresetall`";
    }

    private static string BuildLevelingConfigContent(string prefix)
    {
        return
            "## Leveling — Configuration\n\n" +
            $"`{prefix}levelenable`, `{prefix}leveldisable`, `{prefix}levelconfig`, `{prefix}levelrole`, `{prefix}blacklistchannel`, `{prefix}blacklistrole`, `{prefix}setcooldown`, `{prefix}setxprate`, `{prefix}setrankchannel`, `{prefix}setlevelupmessage`, `{prefix}togglelevelup`";
    }

    private static string BuildSetupRoleStaffContent(string prefix)
    {
        return
            "## Setup-Roles — Staff Access\n\n" +
            $"`{prefix}setuprole add`, `{prefix}setuprole remove`, `{prefix}setuprole list`\n\n" +
            "-# Staff roles allowed to use the server's dynamic role commands • " +
            $"`{prefix}setuprole add @role`";
    }

    private static string BuildSetupRoleCommandsContent(string prefix)
    {
        return
            "## Setup-Roles — Custom Commands\n\n" +
            $"`{prefix}setuprolecreate`, `{prefix}setuproleshow`\n\n" +
            $"-# `{prefix}setuprolecreate vip @VIP` makes `{prefix}vip @user` toggle that role • " +
            $"`{prefix}setuprolecreate remove vip` deletes it";
    }

    private static string BuildWelcomeSetupContent(string prefix)
    {
        return
            "## Welcome — Setup\n\n" +
            $"`{prefix}welcomechannel set`, `{prefix}welcomechannel remove`, " +
            $"`{prefix}welcome on`, `{prefix}welcome off`, `{prefix}welcome test`\n\n" +
            $"-# Greets every new member with a card • `{prefix}welcomechannel set #channel` " +
            $"then `{prefix}welcome on` • alias `{prefix}greet`";
    }

    private static string BuildWelcomeMessageContent(string prefix)
    {
        return
            "## Welcome — Message\n\n" +
            $"`{prefix}welcomemessage`, `{prefix}welcome status`, `{prefix}welcome reset`\n\n" +
            "-# Placeholders `{user}`, `{username}`, `{server}`, `{membercount}` • " +
            $"`{prefix}welcomemessage reset` restores the default greeting";
    }

    private static string BuildTicketSetupContent(string prefix)
    {
        return
            "## Ticket — Setup\n\n" +
            $"`{prefix}ticketsetup`, `{prefix}ticketpanel`, `{prefix}ticketcategory`, " +
            $"`{prefix}ticketlogs`, `{prefix}ticketrole`, `{prefix}ticketmessage`, " +
            $"`{prefix}ticketlimit`, `{prefix}ticket on`, `{prefix}ticket off`, " +
            $"`{prefix}ticket status`, `{prefix}ticket reset`\n\n" +
            $"-# Run `{prefix}ticketsetup` once, then `{prefix}ticketpanel #channel` " +
            $"to post the Create Ticket button • alias `{prefix}tickets`";
    }

    private static string BuildTicketUseContent(string prefix)
    {
        return
            "## Ticket — Members\n\n" +
            $"`{prefix}new`, `{prefix}close`\n\n" +
            $"-# `{prefix}new <reason>` opens a private channel with staff • " +
            $"`{prefix}close <reason>` closes it and saves a transcript";
    }

    private static string BuildTicketStaffContent(string prefix)
    {
        return
            "## Ticket — Staff\n\n" +
            $"`{prefix}claim`, `{prefix}unclaim`, `{prefix}ticketadd`, " +
            $"`{prefix}ticketremove`, `{prefix}ticketrename`, `{prefix}reopen`, " +
            $"`{prefix}ticketdelete`, `{prefix}transcript`, `{prefix}ticketlist`\n\n" +
            $"-# Needs a support role from `{prefix}ticketrole add @role`, " +
            "`Manage Channels`, or `Administrator`";
    }

    private static string BuildMediaChannelsContent(string prefix)
    {
        return
            "## Media — Channels\n\n" +
            $"`{prefix}media setup`, `{prefix}media remove`, `{prefix}media show`, " +
            $"`{prefix}media on`, `{prefix}media off`, `{prefix}media reset`\n\n" +
            $"-# Turns a channel media-only • `{prefix}media setup #channel [#channel …]` " +
            $"adds several at once • aliases `{prefix}mediaonly`, `{prefix}mediachannel`";
    }

    private static string BuildMediaForwardContent(string prefix)
    {
        return
            "## Media — Enforcement & Forwarding\n\n" +
            $"`{prefix}media chat set`, `{prefix}media chat remove`\n\n" +
            "-# Only images, videos, files, stickers and links survive — everything " +
            "else is removed, commands included, and nobody is exempt\n" +
            "-# A removed message that mentions someone is forwarded to the chat " +
            "channel and pings them there\n" +
            "-# Run these commands outside a media-only channel, they are removed " +
            "there too • needs `Manage Server` or `Administrator`";
    }

    private static string BuildMusicPlaybackContent(string prefix)
    {
        return
            "## Music — Playback\n\n" +
            $"`{prefix}play`, `{prefix}pause`, `{prefix}resume`, `{prefix}skip`, " +
            $"`{prefix}stop`, `{prefix}nowplaying`, `{prefix}volume`, `{prefix}seek`, " +
            $"`{prefix}join`, `{prefix}leave`";
    }

    private static string BuildMusicQueueContent(string prefix)
    {
        return
            "## Music — Queue\n\n" +
            $"`{prefix}queue`, `{prefix}loop`, `{prefix}shuffle`, `{prefix}remove`, " +
            $"`{prefix}clear`";
    }

    private static string BuildGiveawayHostContent(string prefix)
    {
        return
            "## Giveaway — Hosting\n\n" +
            $"`{prefix}gstart`, `{prefix}gend`, `{prefix}greroll`\n\n" +
            $"-# `{prefix}gstart <duration> [winners]w <prize>` • e.g. " +
            $"`{prefix}gstart 1h Nitro` or `{prefix}gstart 12h 3w Nitro Classic`\n" +
            $"-# `{prefix}gend` ends one early and draws now • `{prefix}greroll` " +
            "draws again and never repeats an earlier winner\n" +
            $"-# ID optional when only one giveaway fits • needs `Manage Server` " +
            "or `Administrator`";
    }

    private static string BuildGiveawayInfoContent(string prefix)
    {
        return
            "## Giveaway — Info\n\n" +
            $"`{prefix}glist`, `{prefix}gentries`\n\n" +
            "-# Members join by pressing the 🎉 Enter button on the card, and " +
            "pressing it again leaves\n" +
            $"-# `{prefix}glist` shows every running giveaway with jump links and " +
            $"IDs • `{prefix}gentries` pages through who entered\n" +
            "-# Both are open to everyone • giveaways survive a restart and end on " +
            "time by themselves";
    }

    private static string BuildLogsContent(string prefix)
    {
        return
            "## Logs commands\n\n" +
            $"`{prefix}logenable`, `{prefix}logconfig`, `{prefix}logset`, `{prefix}logdisable`, `{prefix}logreset`";
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
        var levelingValue = HelpComponentIds.ToValue(HelpCategory.Leveling);
        var setupRolesValue = HelpComponentIds.ToValue(HelpCategory.SetupRoles);
        var welcomeValue = HelpComponentIds.ToValue(HelpCategory.Welcome);
        var ticketValue = HelpComponentIds.ToValue(HelpCategory.Ticket);
        var mediaValue = HelpComponentIds.ToValue(HelpCategory.Media);
        var musicValue = HelpComponentIds.ToValue(HelpCategory.Music);
        var giveawayValue = HelpComponentIds.ToValue(HelpCategory.Giveaway);
        var logsValue = HelpComponentIds.ToValue(HelpCategory.Logs);
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
                "Leveling",
                levelingValue,
                "XP, ranks, roles & leaderboards",
                isDefault: selectedCategory == HelpCategory.Leveling)
            .AddOption(
                "Setup-Roles",
                setupRolesValue,
                "Custom role-assignment commands",
                isDefault: selectedCategory == HelpCategory.SetupRoles)
            .AddOption(
                "Welcome",
                welcomeValue,
                "Greet new members on join",
                isDefault: selectedCategory == HelpCategory.Welcome)
            .AddOption(
                "Ticket",
                ticketValue,
                "Private support ticket channels",
                isDefault: selectedCategory == HelpCategory.Ticket)
            .AddOption(
                "Media",
                mediaValue,
                "Media-only channels & forwarding",
                isDefault: selectedCategory == HelpCategory.Media)
            .AddOption(
                "Music",
                musicValue,
                "Play music in voice channels",
                isDefault: selectedCategory == HelpCategory.Music)
            .AddOption(
                "Giveaway",
                giveawayValue,
                "Host giveaways with entry buttons",
                isDefault: selectedCategory == HelpCategory.Giveaway)
            .AddOption(
                "Logs",
                logsValue,
                "Server event logging channels",
                isDefault: selectedCategory == HelpCategory.Logs)
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
