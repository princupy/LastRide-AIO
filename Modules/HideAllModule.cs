using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Core;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed class HideAllModule : ModuleBase<SocketCommandContext>
{
    private const string DefaultReason = "No reason provided.";
    private readonly HideAllComponentBuilder _builder;

    public HideAllModule(HideAllComponentBuilder builder)
    {
        _builder = builder;
    }

    [Command("hideall")]
    [Summary("Hides every server channel from everyone.")]
    public async Task HideAllAsync([Remainder] string? reason = null)
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
        var hiddenCount = 0;
        var alreadyHiddenCount = 0;
        var failedCount = 0;

        foreach (var channel in Context.Guild.Channels)
        {
            var currentOverwrite = channel.GetPermissionOverwrite(everyoneRole);

            if (currentOverwrite?.ViewChannel == PermValue.Deny)
            {
                alreadyHiddenCount++;
                continue;
            }

            try
            {
                var overwrite =
                    currentOverwrite ?? OverwritePermissions.InheritAll;

                await channel.AddPermissionOverwriteAsync(
                    everyoneRole,
                    overwrite.Modify(viewChannel: PermValue.Deny),
                    new RequestOptions
                    {
                        AuditLogReason =
                            $"Hide all by {moderator.Username}: {reasonText}"
                    });

                hiddenCount++;
            }
            catch (Exception exception)
            {
                failedCount++;
                Console.WriteLine(
                    $"[HideAll Error] #{channel.Name} ({channel.Id}): {DiscordFailure.Summarize(exception)}");
            }
        }

        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildSuccess(
                hiddenCount,
                alreadyHiddenCount,
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
