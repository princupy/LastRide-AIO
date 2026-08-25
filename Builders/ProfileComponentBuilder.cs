using Discord;
using Discord.WebSocket;

namespace LastRide.Builders;

public sealed class ProfileComponentBuilder
{
    private static readonly Color AccentColor = new(8, 4, 4);

    public MessageComponent BuildAvatar(
        IUser targetUser,
        SocketGuildUser? guildUser,
        ulong requesterId,
        ulong guildId,
        AvatarView selectedView)
    {
        var globalAvatarUrl =
            targetUser.GetAvatarUrl(size: 2048) ??
            targetUser.GetDefaultAvatarUrl();

        var serverAvatarUrl = guildUser?.GetGuildAvatarUrl(size: 2048);
        var selectedAvatarUrl =
            selectedView == AvatarView.Server && serverAvatarUrl is not null
                ? serverAvatarUrl
                : globalAvatarUrl;

        var selectedLabel =
            selectedView == AvatarView.Server && serverAvatarUrl is not null
                ? "Server PFP"
                : "Global PFP";

        var content =
            $"## {EscapeInlineCode(GetDisplayName(targetUser))}'s Avatar\n" +
            $"> **Viewing:** `{selectedLabel}`\n" +
            $"> [Open Image]({selectedAvatarUrl})";

        var container = new ContainerBuilder()
            .WithAccentColor(AccentColor)
            .AddComponents(
                new TextDisplayBuilder(content),
                new MediaGalleryBuilder()
                    .AddItem(selectedAvatarUrl, selectedLabel, false),
                Divider(isDivider: false),
                BuildAvatarButtons(
                    selectedView,
                    requesterId,
                    targetUser.Id,
                    guildId,
                    serverAvatarUrl is not null),
                FooterSeparator(),
                new TextDisplayBuilder(ComponentFooter.Text));

        return new ComponentBuilderV2()
            .AddComponent(container)
            .Build();
    }

    public MessageComponent BuildBanner(
        IUser targetUser,
        string? bannerUrl)
    {
        var title = $"## {EscapeInlineCode(GetDisplayName(targetUser))}'s Banner";

        var container = new ContainerBuilder()
            .WithAccentColor(AccentColor);

        if (string.IsNullOrWhiteSpace(bannerUrl))
        {
            container.AddComponent(
                new TextDisplayBuilder(
                    $"{title}\n> This user does not have a visible banner."));
        }
        else
        {
            container.AddComponents(
                new TextDisplayBuilder(
                    $"{title}\n> [Open Image]({bannerUrl})"),
                new MediaGalleryBuilder()
                    .AddItem(bannerUrl, "Banner", false));
        }

        container.AddComponents(
            FooterSeparator(),
            new TextDisplayBuilder(ComponentFooter.Text));

        return new ComponentBuilderV2()
            .AddComponent(container)
            .Build();
    }

    private static ActionRowBuilder BuildAvatarButtons(
        AvatarView selectedView,
        ulong requesterId,
        ulong targetUserId,
        ulong guildId,
        bool hasServerAvatar)
    {
        return new ActionRowBuilder()
            .WithButton(
                ButtonBuilder
                    .CreateSecondaryButton(
                        "Global PFP",
                        ProfileComponentIds.Create(
                            AvatarView.Global,
                            requesterId,
                            targetUserId,
                            guildId))
                    .WithDisabled(selectedView == AvatarView.Global))
            .WithButton(
                ButtonBuilder
                    .CreateSecondaryButton(
                        "Server PFP",
                        ProfileComponentIds.Create(
                            AvatarView.Server,
                            requesterId,
                            targetUserId,
                            guildId))
                    .WithDisabled(
                        !hasServerAvatar ||
                        selectedView == AvatarView.Server));
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

    private static string GetDisplayName(IUser user)
    {
        return string.IsNullOrWhiteSpace(user.GlobalName)
            ? user.Username
            : user.GlobalName;
    }

    private static string EscapeInlineCode(string value)
    {
        return value.Replace("`", "'");
    }
}
