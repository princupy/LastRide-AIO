using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Core;
using LastRide.Services;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed class MuteModule : ModuleBase<SocketCommandContext>
{
    private const string DefaultReason = "No reason provided.";
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MaxDuration = TimeSpan.FromDays(28);
    private readonly MuteComponentBuilder _builder;
    private readonly LogService _logService;

    public MuteModule(
        MuteComponentBuilder builder,
        LogService logService)
    {
        _builder = builder;
        _logService = logService;
    }

    [Command("mute")]
    [Summary("Times out a member for a given duration.")]
    public async Task MuteAsync([Remainder] string? input = null)
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
            !HasModeratePermission(moderator.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "You need `Timeout Members` or `Administrator` permission to use this command.");
            return;
        }

        if (!HasModeratePermission(Context.Guild.CurrentUser.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Bot Permission",
                "I need `Timeout Members` or `Administrator` permission to mute users.");
            return;
        }

        var parsed = ParseInput(input);

        if (parsed is null)
        {
            await ReplyNoticeAsync(
                "Invalid Usage",
                "Usage: `?mute @user 10m reason`. The member has to be mentioned.");
            return;
        }

        if (!UserReference.TryParse(parsed.Value.Target, out var targetId))
        {
            await ReplyNoticeAsync(
                "Mention Required",
                "Mention the member you want to mute: `?mute @user 10m reason`. " +
                "A user ID works too, but a plain name does not — I will not guess who " +
                "you meant.");
            return;
        }

        var target = Context.Guild.GetUser(targetId);

        if (target is null)
        {
            await ReplyNoticeAsync(
                "User Not Found",
                "That user is not a member of this server.");
            return;
        }

        var hierarchyError = ValidateHierarchy(
            Context.Guild,
            moderator,
            target);

        if (hierarchyError is not null)
        {
            await ReplyNoticeAsync("Cannot Mute", hierarchyError);
            return;
        }

        if (target.TimedOutUntil is { } until &&
            until > DateTimeOffset.UtcNow)
        {
            await ReplyNoticeAsync(
                "Already Muted",
                $"<@{target.Id}> is already muted until <t:{until.ToUnixTimeSeconds()}:R>.");
            return;
        }

        var duration = parsed.Value.Duration;
        var reasonText = parsed.Value.Reason;
        var expiresUnix = DateTimeOffset.UtcNow.Add(duration).ToUnixTimeSeconds();

        try
        {
            await target.SetTimeOutAsync(
                duration,
                new RequestOptions
                {
                    AuditLogReason = $"Muted by {moderator.Username}: {reasonText}"
                });

            await ReplyAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildMuted(
                    target.Id,
                    target.DisplayName,
                    target.GetDisplayAvatarUrl(size: 256),
                    FormatDuration(duration),
                    expiresUnix,
                    reasonText,
                    moderator.Id));

            await _logService.LogMuteAsync(
                Context.Guild,
                target,
                moderator,
                duration,
                reasonText);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Mute Error] {DiscordFailure.Format(exception)}");

            await ReplyNoticeAsync(
                "Mute Failed",
                DiscordFailure.Describe(
                    exception,
                    "I could not mute this member. Check my permissions and role position."));
        }
    }

    private async Task ReplyNoticeAsync(string title, string message)
    {
        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildNotice(title, message));
    }

    private static string? ValidateHierarchy(
        SocketGuild guild,
        SocketGuildUser moderator,
        SocketGuildUser target)
    {
        if (target.Id == moderator.Id)
            return "You cannot mute yourself.";

        if (target.Id == guild.CurrentUser.Id)
            return "I cannot mute myself.";

        if (target.Id == guild.OwnerId)
            return "The server owner cannot be muted.";

        if (moderator.Id != guild.OwnerId &&
            target.Hierarchy >= moderator.Hierarchy)
        {
            return "You cannot mute a member with an equal or higher role.";
        }

        if (target.Hierarchy >= guild.CurrentUser.Hierarchy)
            return "My highest role must be above the target member's highest role.";

        return null;
    }

    private static bool HasModeratePermission(GuildPermissions permissions)
    {
        return permissions.ModerateMembers || permissions.Administrator;
    }

    private static ParsedMuteInput? ParseInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var trimmed = input.Trim();
        var firstSpace = trimmed.IndexOf(' ');

        if (firstSpace < 0)
        {
            return new ParsedMuteInput(trimmed, DefaultDuration, DefaultReason);
        }

        var target = trimmed[..firstSpace].Trim();
        var rest = trimmed[(firstSpace + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(target))
            return null;

        var secondSpace = rest.IndexOf(' ');
        var durationToken = secondSpace < 0
            ? rest
            : rest[..secondSpace];

        if (TryParseDuration(durationToken, out var duration))
        {
            var reason = secondSpace < 0
                ? string.Empty
                : rest[(secondSpace + 1)..].Trim();

            return new ParsedMuteInput(
                target,
                duration,
                string.IsNullOrWhiteSpace(reason) ? DefaultReason : reason);
        }

        return new ParsedMuteInput(
            target,
            DefaultDuration,
            string.IsNullOrWhiteSpace(rest) ? DefaultReason : rest);
    }

    private static bool TryParseDuration(string token, out TimeSpan duration)
    {
        duration = DefaultDuration;

        if (string.IsNullOrWhiteSpace(token) || token.Length < 2)
            return false;

        var unit = char.ToLowerInvariant(token[^1]);
        var numberPart = token[..^1];

        if (!int.TryParse(numberPart, out var amount) || amount <= 0)
            return false;

        var parsed = unit switch
        {
            's' => TimeSpan.FromSeconds(amount),
            'm' => TimeSpan.FromMinutes(amount),
            'h' => TimeSpan.FromHours(amount),
            'd' => TimeSpan.FromDays(amount),
            _ => TimeSpan.Zero
        };

        if (parsed <= TimeSpan.Zero)
            return false;

        duration = parsed > MaxDuration ? MaxDuration : parsed;
        return true;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
            return $"{duration.TotalDays:0.##}d";

        if (duration.TotalHours >= 1)
            return $"{duration.TotalHours:0.##}h";

        if (duration.TotalMinutes >= 1)
            return $"{duration.TotalMinutes:0.##}m";

        return $"{duration.TotalSeconds:0.##}s";
    }

    private readonly record struct ParsedMuteInput(
        string Target,
        TimeSpan Duration,
        string Reason);
}
