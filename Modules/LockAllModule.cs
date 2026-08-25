using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed class LockAllModule : ModuleBase<SocketCommandContext>
{
    private const string DefaultReason = "No reason provided.";
    private readonly LockAllComponentBuilder _builder;

    public LockAllModule(LockAllComponentBuilder builder)
    {
        _builder = builder;
    }

    [Command("lockall")]
    [Summary("Locks every server channel so members cannot send messages.")]
    public async Task LockAllAsync([Remainder] string? reason = null)
    {
        if (Context.Guild is null)
        {
            await ReplyNoticeAsync(
                "Server Only",
                "This command can only be used in a server.");
            return;
        }

        var moderator = Context.User as SocketGuildUser;

        if (moderator is null ||
            !HasManageChannels(moderator.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "You need `Manage Channels` or `Administrator` permission to use this command.");
            return;
        }

        if (!CanEditPermissions(Context.Guild.CurrentUser.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Bot Permission",
                "I need `Manage Roles` or `Administrator` permission to edit channel permissions.");
            return;
        }

        var reasonText = string.IsNullOrWhiteSpace(reason)
            ? DefaultReason
            : reason.Trim();
        var everyoneRole = Context.Guild.EveryoneRole;
        var lockedCount = 0;
        var alreadyLockedCount = 0;
        var failedCount = 0;

        foreach (var channel in Context.Guild.Channels)
        {
            var currentOverwrite = channel.GetPermissionOverwrite(everyoneRole);

            if (currentOverwrite?.SendMessages == PermValue.Deny)
            {
                alreadyLockedCount++;
                continue;
            }

            try
            {
                var overwrite =
                    currentOverwrite ?? OverwritePermissions.InheritAll;

                await channel.AddPermissionOverwriteAsync(
                    everyoneRole,
                    overwrite.Modify(sendMessages: PermValue.Deny),
                    new RequestOptions
                    {
                        AuditLogReason =
                            $"Lock all by {moderator.Username}: {reasonText}"
                    });

                lockedCount++;
            }
            catch (Exception exception)
            {
                failedCount++;
                Console.WriteLine(
                    $"[LockAll Error] #{channel.Name} ({channel.Id}): {exception.Message}");
            }
        }

        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildSuccess(
                lockedCount,
                alreadyLockedCount,
                failedCount,
                moderator.Id));
    }

    private async Task ReplyNoticeAsync(string title, string message)
    {
        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildNotice(title, message));
    }

    private static bool HasManageChannels(GuildPermissions permissions)
    {
        return permissions.ManageChannels || permissions.Administrator;
    }

    private static bool CanEditPermissions(GuildPermissions permissions)
    {
        return permissions.ManageRoles || permissions.Administrator;
    }
}
