using Discord;

namespace LastRide.Builders;

public sealed class AddRoleComponentBuilder
{
    private static readonly Color AccentColor = new(8, 4, 4);

    public MessageComponent BuildSuccess(
        ulong targetId,
        string targetName,
        string? targetAvatarUrl,
        string roleName,
        ulong roleId,
        ulong moderatorId)
    {
        var content =
            $"> **Member:** <@{targetId}>\n" +
            $"> **Role:** `{EscapeInlineCode(roleName)}`\n" +
            $"> **Role ID:** `{roleId}`\n" +
            $"> **Moderator:** <@{moderatorId}>";

        return BuildContainer(
            BuildHeader("Role Added", content, targetAvatarUrl, targetName));
    }

    public MessageComponent BuildRemoved(
        ulong targetId,
        string targetName,
        string? targetAvatarUrl,
        string roleName,
        ulong roleId,
        ulong moderatorId)
    {
        var content =
            $"> **Member:** <@{targetId}>\n" +
            $"> **Role:** `{EscapeInlineCode(roleName)}`\n" +
            $"> **Role ID:** `{roleId}`\n" +
            $"> **Moderator:** <@{moderatorId}>";

        return BuildContainer(
            BuildHeader("Role Removed", content, targetAvatarUrl, targetName));
    }

    public MessageComponent BuildAlreadyHasRole(
        ulong targetId,
        string roleName,
        ulong roleId,
        ulong requesterId,
        ulong guildId)
    {
        var content =
            "## Already Has Role\n" +
            $"> <@{targetId}> already has the `{EscapeInlineCode(roleName)}` role.";

        return BuildContainer(
            new TextDisplayBuilder(content),
            Divider(isDivider: false),
            BuildRemoveButton(requesterId, guildId, targetId, roleId));
    }

    public MessageComponent BuildNotice(
        string title,
        string message)
    {
        return BuildContainer(
            new TextDisplayBuilder(
                $"## {EscapeMarkdown(title)}\n> {message}"));
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

    private static ActionRowBuilder BuildRemoveButton(
        ulong requesterId,
        ulong guildId,
        ulong targetId,
        ulong roleId)
    {
        return new ActionRowBuilder()
            .WithButton(
                ButtonBuilder.CreateDangerButton(
                    "Remove Role",
                    AddRoleComponentIds.CreateRemove(
                        requesterId,
                        guildId,
                        targetId,
                        roleId)));
    }

    private static SeparatorBuilder Divider(bool isDivider = true)
    {
        return new SeparatorBuilder(
            isDivider: isDivider,
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
