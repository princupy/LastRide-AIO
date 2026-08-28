using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Core;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed class NickModule : ModuleBase<SocketCommandContext>
{
    private const int MaxNicknameLength = 32;
    private readonly NickComponentBuilder _builder;

    public NickModule(NickComponentBuilder builder)
    {
        _builder = builder;
    }

    [Command("nick")]
    [Alias("nickname", "setnick")]
    [Summary("Changes or resets a member's nickname.")]
    public async Task NickAsync([Remainder] string? input = null)
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
            !HasManageNicknames(moderator.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "You need `Manage Nicknames` or `Administrator` permission to use this command.");
            return;
        }

        if (!HasManageNicknames(Context.Guild.CurrentUser.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Bot Permission",
                "I need `Manage Nicknames` or `Administrator` permission to change nicknames.");
            return;
        }

        var parsed = ParseInput(input);

        if (parsed is null)
        {
            await ReplyNoticeAsync(
                "Invalid Usage",
                "Usage: `?nick @user NewName` or `?nick user_id` to reset.");
            return;
        }

        var target = ResolveTarget(parsed.Value.Target);

        if (target is null)
        {
            await ReplyNoticeAsync(
                "User Not Found",
                "I could not find that member. Mention them or provide a valid user ID.");
            return;
        }

        var hierarchyError = ValidateHierarchy(
            Context.Guild,
            moderator,
            target);

        if (hierarchyError is not null)
        {
            await ReplyNoticeAsync("Cannot Change Nickname", hierarchyError);
            return;
        }

        var newNickname = parsed.Value.Nickname;

        if (newNickname is not null && newNickname.Length > MaxNicknameLength)
        {
            await ReplyNoticeAsync(
                "Nickname Too Long",
                $"Nicknames must be `{MaxNicknameLength}` characters or fewer.");
            return;
        }

        var oldNickname = target.Nickname;

        try
        {
            await target.ModifyAsync(
                properties => properties.Nickname = newNickname,
                new RequestOptions
                {
                    AuditLogReason = $"Nickname changed by {moderator.Username}"
                });

            await ReplyAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildChanged(
                    target.Id,
                    target.DisplayName,
                    target.GetDisplayAvatarUrl(size: 256),
                    oldNickname,
                    newNickname,
                    moderator.Id));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Nick Error] {DiscordFailure.Format(exception)}");

            await ReplyNoticeAsync(
                "Nickname Change Failed",
                DiscordFailure.Describe(
                    exception,
                    "I could not change this member's nickname. Check my permissions and role position."));
        }
    }

    private async Task ReplyNoticeAsync(string title, string message)
    {
        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildNotice(title, message));
    }

    /// <summary>
    /// Only an explicit reference counts — see <see cref="UserReference"/> for why a plain
    /// name is refused rather than matched.
    /// </summary>
    private SocketGuildUser? ResolveTarget(string query)
    {
        return UserReference.TryParse(query, out var userId)
            ? Context.Guild.GetUser(userId)
            : null;
    }

    private static string? ValidateHierarchy(
        SocketGuild guild,
        SocketGuildUser moderator,
        SocketGuildUser target)
    {
        if (target.Id == guild.CurrentUser.Id)
            return "I cannot change my own nickname with this command.";

        if (target.Id == guild.OwnerId)
            return "The server owner's nickname cannot be changed.";

        if (target.Id == moderator.Id)
            return null;

        if (moderator.Id != guild.OwnerId &&
            target.Hierarchy >= moderator.Hierarchy)
        {
            return "You cannot change the nickname of a member with an equal or higher role.";
        }

        if (target.Hierarchy >= guild.CurrentUser.Hierarchy)
            return "My highest role must be above the target member's highest role.";

        return null;
    }

    private static bool HasManageNicknames(GuildPermissions permissions)
    {
        return permissions.ManageNicknames || permissions.Administrator;
    }

    private static ParsedNickInput? ParseInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var trimmed = input.Trim();
        var separatorIndex = trimmed.IndexOf(' ');

        if (separatorIndex < 0)
        {
            return new ParsedNickInput(trimmed, null);
        }

        var target = trimmed[..separatorIndex].Trim();
        var nickname = trimmed[(separatorIndex + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(target))
            return null;

        return new ParsedNickInput(
            target,
            string.IsNullOrWhiteSpace(nickname) ? null : nickname);
    }

    private readonly record struct ParsedNickInput(
        string Target,
        string? Nickname);
}
