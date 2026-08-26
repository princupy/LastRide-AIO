using Discord;
using Discord.WebSocket;

namespace LastRide.Services;

/// <summary>
/// Applies auto-roles when members join and grants/removes the voice role as
/// members enter and leave voice channels. Wired to the gateway events by
/// <see cref="Core.CommandHandler"/>.
/// </summary>
public sealed class AutoRoleService
{
    private readonly AutoRoleConfigService _configService;

    public AutoRoleService(AutoRoleConfigService configService)
    {
        _configService = configService;
    }

    public async Task HandleUserJoinedAsync(SocketGuildUser user)
    {
        try
        {
            var config = _configService.GetConfig(user.Guild.Id);

            if (!config.AutoRoleEnabled)
                return;

            // Bots and humans draw from separate lists.
            var roleIds = user.IsBot ? config.BotRoleIds : config.HumanRoleIds;

            if (roleIds.Count == 0)
                return;

            foreach (var roleId in roleIds)
            {
                await TryAddRoleAsync(
                    user.Guild,
                    user,
                    roleId,
                    "AutoRole: role granted on join.");
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[AutoRole Join Error] {exception}");
        }
    }

    public async Task HandleVoiceStateUpdatedAsync(
        SocketUser socketUser,
        SocketVoiceState before,
        SocketVoiceState after)
    {
        try
        {
            // Only real members get the voice role; bots (music/stage) are left alone.
            if (socketUser is not SocketGuildUser user || user.IsBot)
                return;

            var config = _configService.GetConfig(user.Guild.Id);

            if (!config.VcRoleEnabled || config.VcRoleId is not { } roleId)
                return;

            var joinedVoice =
                before.VoiceChannel is null && after.VoiceChannel is not null;
            var leftVoice =
                before.VoiceChannel is not null && after.VoiceChannel is null;

            // Moving between channels keeps both sides non-null, so the role is
            // neither added nor removed — the member already holds it.
            if (joinedVoice)
            {
                await TryAddRoleAsync(
                    user.Guild,
                    user,
                    roleId,
                    "VCRole: joined a voice channel.");
            }
            else if (leftVoice)
            {
                await TryRemoveRoleAsync(
                    user.Guild,
                    user,
                    roleId,
                    "VCRole: left the voice channel.");
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[VCRole Voice Error] {exception}");
        }
    }

    private static async Task TryAddRoleAsync(
        SocketGuild guild,
        SocketGuildUser user,
        ulong roleId,
        string reason)
    {
        var role = guild.GetRole(roleId);

        if (!CanAssignRole(guild, role))
            return;

        if (user.Roles.Any(existing => existing.Id == roleId))
            return;

        try
        {
            await user.AddRoleAsync(
                role,
                new RequestOptions { AuditLogReason = reason });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[AutoRole Add Error] {roleId}: {exception.Message}");
        }
    }

    private static async Task TryRemoveRoleAsync(
        SocketGuild guild,
        SocketGuildUser user,
        ulong roleId,
        string reason)
    {
        var role = guild.GetRole(roleId);

        if (role is null)
            return;

        if (user.Roles.All(existing => existing.Id != roleId))
            return;

        try
        {
            await user.RemoveRoleAsync(
                role,
                new RequestOptions { AuditLogReason = reason });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[VCRole Remove Error] {roleId}: {exception.Message}");
        }
    }

    private static bool CanAssignRole(SocketGuild guild, SocketRole? role)
    {
        if (role is null)
            return false;

        if (role.Id == guild.EveryoneRole.Id || role.IsManaged)
            return false;

        if (!guild.CurrentUser.GuildPermissions.ManageRoles &&
            !guild.CurrentUser.GuildPermissions.Administrator)
        {
            return false;
        }

        // The bot can only grant roles positioned below its own highest role.
        return role.Position < guild.CurrentUser.Hierarchy;
    }
}
