using Discord;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Core;

namespace LastRide.Services;

/// <summary>
/// Executes the dynamic role commands created with <c>setuprolecreate</c>.
/// Discord.Net's command service only knows the commands declared at compile
/// time, so these guild-defined names are dispatched manually from
/// <see cref="Core.CommandHandler"/> once a message turns out not to match a
/// built-in command.
/// </summary>
public sealed class SetupRoleService
{
    private readonly SetupRoleConfigService _configService;
    private readonly SetupRoleComponentBuilder _builder;
    private readonly PrefixService _prefixService;

    public SetupRoleService(
        SetupRoleConfigService configService,
        SetupRoleComponentBuilder builder,
        PrefixService prefixService)
    {
        _configService = configService;
        _builder = builder;
        _prefixService = prefixService;
    }

    /// <summary>
    /// Returns <c>true</c> when the message was a dynamic role command and has
    /// been dealt with, so the caller stays quiet about the unknown command.
    /// </summary>
    public async Task<bool> TryHandleCommandAsync(
        SocketUserMessage message,
        string commandName,
        int argumentPosition)
    {
        try
        {
            if (message.Channel is not SocketGuildChannel guildChannel)
                return false;

            if (message.Author is not SocketGuildUser member || member.IsBot)
                return false;

            var name = SetupRoleConfigService.NormalizeCommandName(commandName);

            if (name.Length == 0)
                return false;

            var guild = guildChannel.Guild;
            var config = _configService.GetConfig(guild.Id);

            // Not one of this guild's commands — leave it as an unknown command
            // so nothing is posted.
            if (!config.TryGetCommandRole(name, out var roleId))
                return false;

            var prefix = _prefixService.GetPrefix(guild.Id);

            if (!member.GuildPermissions.Administrator &&
                !config.HasStaffRole(member.Roles.Select(role => role.Id)))
            {
                await ReplyNoticeAsync(
                    message,
                    "Missing Access",
                    "You need one of the configured staff roles to use this " +
                    $"command. Check `{prefix}setuprole list`.");

                return true;
            }

            if (!guild.CurrentUser.GuildPermissions.ManageRoles)
            {
                await ReplyNoticeAsync(
                    message,
                    "Missing Permission",
                    "I need the `Manage Roles` permission to change roles.");

                return true;
            }

            var role = guild.GetRole(roleId);

            if (role is null)
            {
                await ReplyNoticeAsync(
                    message,
                    "Role Missing",
                    "The role behind this command no longer exists. Remove it " +
                    $"with `{prefix}setuprolecreate remove {name}`.");

                return true;
            }

            var arguments = ReadArguments(message.Content, argumentPosition);

            if (arguments.Length == 0)
            {
                await ReplyNoticeAsync(
                    message,
                    "Missing Member",
                    $"Provide a member to toggle the role for — `{prefix}{name} @user`.");

                return true;
            }

            var target = ResolveTarget(guild, arguments);

            if (target is null)
            {
                await ReplyNoticeAsync(
                    message,
                    "Member Not Found",
                    "I could not find that member in this server. Mention them or use " +
                    "their user ID.");

                return true;
            }

            var problem = ValidateRole(guild, role);

            if (problem is not null)
            {
                await ReplyNoticeAsync(message, "Cannot Assign Role", problem);
                return true;
            }

            var hasRole = target.Roles.Any(existing => existing.Id == role.Id);
            var options = new RequestOptions
            {
                AuditLogReason =
                    $"{prefix}{name} command used by {member.Username}"
            };

            try
            {
                if (hasRole)
                {
                    await target.RemoveRoleAsync(role, options);
                }
                else
                {
                    await target.AddRoleAsync(role, options);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[SetupRole Role Error] {DiscordFailure.Format(exception)}");

                await ReplyNoticeAsync(
                    message,
                    "Role Update Failed",
                    DiscordFailure.Describe(
                        exception,
                        "Discord rejected the role change. Check my permissions and " +
                        "role position, then try again."));

                return true;
            }

            await message.Channel.SendMessageAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildRoleToggled(
                    !hasRole,
                    target.Id,
                    target.DisplayName,
                    target.GetDisplayAvatarUrl(size: 256),
                    name,
                    prefix,
                    role.Name,
                    role.Id,
                    member.Id));

            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[SetupRole Error] {DiscordFailure.Format(exception)}");
            return false;
        }
    }

    // Everything after the command name itself — the command word was already
    // matched by the caller, so only the member query is left.
    private static string ReadArguments(string content, int argumentPosition)
    {
        var commandText = content[argumentPosition..].TrimStart();
        var spaceIndex = commandText.IndexOf(' ');

        return spaceIndex < 0
            ? string.Empty
            : commandText[(spaceIndex + 1)..].Trim();
    }

    // Mirrors the member lookup used by the addrole command: an explicit mention or id,
    // nothing else. See UserReference for why name matching was dropped.
    private static SocketGuildUser? ResolveTarget(SocketGuild guild, string query)
    {
        if (!UserReference.TryParse(query, out var userId))
            return null;

        return guild.GetUser(userId);
    }

    // The staff hierarchy check happens when the command is created — whoever
    // set it up already approved this exact role — so only the checks that can
    // change afterwards are re-run here.
    private static string? ValidateRole(SocketGuild guild, SocketRole role)
    {
        if (role.Id == guild.EveryoneRole.Id)
            return "The `@everyone` role cannot be assigned.";

        if (role.IsManaged)
            return "That role is managed by an integration and cannot be assigned.";

        if (role.Position >= guild.CurrentUser.Hierarchy)
            return "My highest role must be above the role this command assigns.";

        return null;
    }

    private Task ReplyNoticeAsync(
        SocketUserMessage message,
        string title,
        string notice)
    {
        return message.Channel.SendMessageAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildNotice(title, notice));
    }
}
