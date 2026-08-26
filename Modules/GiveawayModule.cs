using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Configuration;
using LastRide.Models;
using LastRide.Services;

namespace LastRide.Modules;

[Name("Giveaway")]
public sealed class GiveawayModule : ModuleBase<SocketCommandContext>
{
    private readonly GiveawayService _service;
    private readonly GiveawayComponentBuilder _builder;
    private readonly PrefixService _prefixService;
    private readonly BotOptions _options;

    public GiveawayModule(
        GiveawayService service,
        GiveawayComponentBuilder builder,
        PrefixService prefixService,
        BotOptions options)
    {
        _service = service;
        _builder = builder;
        _prefixService = prefixService;
        _options = options;
    }

    [Command("gstart")]
    [Alias("giveawaystart", "gcreate")]
    [Summary("Start a giveaway with an entry button in this channel.")]
    public async Task StartAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        var parts = Split(input);

        if (parts.Length < 2)
        {
            await ReplyNoticeAsync(
                "Giveaway Start",
                $"Usage: `{Prefix}gstart <duration> [winners]w <prize>`\n" +
                $"> Example: `{Prefix}gstart 1h Nitro`\n" +
                $"> Example: `{Prefix}gstart 12h 3w Nitro Classic`");

            return;
        }

        if (!TryParseDuration(parts[0], out var duration))
        {
            await ReplyNoticeAsync(
                "Invalid Duration",
                $"{Inline(parts[0])} is not a duration — use `30s`, `10m`, `2h`, or `7d`.");

            return;
        }

        if (duration < GiveawayService.MinDuration)
        {
            await ReplyNoticeAsync(
                "Duration Too Short",
                $"A giveaway has to run for at least " +
                $"`{FormatDuration(GiveawayService.MinDuration)}`.");

            return;
        }

        var prizeIndex = 1;
        var winnerCount = 1;

        if (TryParseWinnerCount(parts[1], out var parsedWinners))
        {
            winnerCount = parsedWinners;
            prizeIndex = 2;
        }

        if (winnerCount > GiveawayService.MaxWinners)
        {
            await ReplyNoticeAsync(
                "Too Many Winners",
                $"A giveaway can have at most `{GiveawayService.MaxWinners}` winners.");

            return;
        }

        var prize = string.Join(" ", parts.Skip(prizeIndex));

        if (string.IsNullOrWhiteSpace(prize))
        {
            await ReplyNoticeAsync(
                "Prize Required",
                $"Tell me what is being given away, e.g. `{Prefix}gstart 1h Nitro`.");

            return;
        }

        if (prize.Length > GiveawayService.MaxPrizeLength)
        {
            await ReplyNoticeAsync(
                "Prize Too Long",
                $"Keep the prize under `{GiveawayService.MaxPrizeLength}` characters.");

            return;
        }

        if (Context.User is not SocketGuildUser host ||
            Context.Channel is not SocketTextChannel channel)
        {
            await ReplyNoticeAsync(
                "Unavailable",
                "Giveaways can only be started in a normal server text channel.");

            return;
        }

        var outcome = await _service.StartAsync(
            host,
            channel,
            duration,
            winnerCount,
            prize);

        switch (outcome.Result)
        {
            case GiveawayStartResult.Started when outcome.Giveaway is { } giveaway:
                await ReplyResultAsync(
                    "Giveaway Started",
                    $"Hosting {Inline(prize)} for `{winnerCount}` winner(s), ending in " +
                    $"`{FormatDuration(duration)}`.\n" +
                    $"> Giveaway ID: `{giveaway.MessageId}` • " +
                    $"[jump to giveaway]({giveaway.JumpUrl})",
                    outcome.Persisted);

                return;

            case GiveawayStartResult.LimitReached:
                await ReplyNoticeAsync(
                    "Limit Reached",
                    $"This server already has `{GiveawayService.MaxActivePerGuild}` " +
                    $"giveaways running — end one with `{Prefix}gend` first.");

                return;

            default:
                await ReplyNoticeAsync(
                    "Post Failed",
                    "I could not post the giveaway card here — check my channel " +
                    "permissions and try again.");

                return;
        }
    }

    [Command("gend")]
    [Alias("giveawayend")]
    [Summary("End a running giveaway early and draw the winners now.")]
    public async Task EndAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        if (!TryResolveGiveaway(
                input,
                _service.GetRunning(Context.Guild.Id),
                "running ",
                out var giveaway,
                out var error))
        {
            await ReplyNoticeAsync("Giveaway Not Found", error);
            return;
        }

        await ReplyDrawAsync(await _service.EndAsync(giveaway.MessageId), isReroll: false);
    }

    [Command("greroll")]
    [Alias("giveawayreroll")]
    [Summary("Reroll an ended giveaway and draw a fresh winner.")]
    public async Task RerollAsync([Remainder] string? input = null)
    {
        if (!await EnsureAllowedAsync())
            return;

        if (!TryResolveGiveaway(
                input,
                _service.GetEnded(Context.Guild.Id),
                "ended ",
                out var giveaway,
                out var error))
        {
            await ReplyNoticeAsync("Giveaway Not Found", error);
            return;
        }

        await ReplyDrawAsync(await _service.RerollAsync(giveaway.MessageId), isReroll: true);
    }

    [Command("glist")]
    [Alias("giveaways", "giveawaylist")]
    [Summary("List all running giveaways in this server with jump links.")]
    public async Task ListAsync()
    {
        if (!await EnsureGuildAsync())
            return;

        await ReplyComponentsAsync(
            _builder.BuildList(
                _service.GetRunning(Context.Guild.Id),
                Context.Guild,
                Prefix));
    }

    [Command("gentries")]
    [Alias("gusers", "giveawayentries")]
    [Summary("List every member who has entered a giveaway.")]
    public async Task EntriesAsync([Remainder] string? input = null)
    {
        if (!await EnsureGuildAsync())
            return;

        if (!TryResolveGiveaway(
                input,
                _service.GetAll(Context.Guild.Id),
                string.Empty,
                out var giveaway,
                out var error))
        {
            await ReplyNoticeAsync("Giveaway Not Found", error);
            return;
        }

        await ReplyComponentsAsync(
            _builder.BuildEntries(giveaway, Context.Guild, 0, Context.User.Id));
    }

    [Command("setwinner")]
    [Summary("Set the winner of a giveaway.")]
    [Remarks(HelpComponentBuilder.HiddenCommandRemark)]
    public async Task SetWinnerAsync([Remainder] string? input = null)
    {
        // Nobody but the owner gets any reply at all. Even a "missing permission"
        // card would tell the channel this command exists, and it is meant to stay
        // completely invisible.
        if (_options.OwnerId is not { } ownerId || Context.User.Id != ownerId)
            return;

        if (Context.Guild is null)
            return;

        // The rig must leave nothing behind in the channel, so the invoking message
        // goes first — even when the arguments turn out to be wrong.
        await DeleteInvocationAsync();

        var parts = Split(input);

        if (parts.Length == 0)
        {
            await DmNoticeAsync(
                "Winner Setup",
                $"Usage: `{Prefix}setwinner <@user|user id|clear> [giveaway id]`");

            return;
        }

        var isClearing = parts[0] is "clear" or "reset" or "none" or "off";
        var giveawayToken = parts.Length > 1 ? parts[1] : null;

        if (!TryResolveGiveaway(
                giveawayToken,
                _service.GetAll(Context.Guild.Id),
                string.Empty,
                out var giveaway,
                out var error))
        {
            await DmNoticeAsync("Giveaway Not Found", error);
            return;
        }

        ulong? targetId = null;

        if (!isClearing)
        {
            var target = ResolveTarget(parts[0]);

            if (target is null)
            {
                await DmNoticeAsync(
                    "Member Not Found",
                    $"Could not find {Inline(parts[0])} in that server.");

                return;
            }

            targetId = target.Id;
        }

        if (!await _service.SetRiggedWinnerAsync(giveaway.MessageId, targetId))
        {
            await DmNoticeAsync(
                "Giveaway Not Found",
                "That giveaway is no longer being tracked.");

            return;
        }

        var updated = _service.GetGiveaway(giveaway.MessageId) ?? giveaway;

        await DmComponentsAsync(
            _builder.BuildRigConfirmation(updated, Context.Guild, targetId));
    }

    private async Task ReplyDrawAsync(GiveawayDrawOutcome outcome, bool isReroll)
    {
        switch (outcome.Result)
        {
            case GiveawayDrawResult.Done:
                await ReplyResultAsync(
                    isReroll ? "Giveaway Rerolled" : "Giveaway Ended",
                    $"Drew `{outcome.Giveaway?.WinnerIds.Count ?? 0}` winner(s) — " +
                    "the announcement is right above this message.",
                    _service.IsPersistent);

                return;

            case GiveawayDrawResult.NoWinners:
                await ReplyNoticeAsync(
                    isReroll ? "Nothing To Reroll" : "No Winners",
                    isReroll
                        ? "Everyone who entered has already won this giveaway, so " +
                          "there is nobody left to draw."
                        : "Nobody eligible entered, so no winner could be drawn.");

                return;

            case GiveawayDrawResult.AlreadyEnded:
                await ReplyNoticeAsync(
                    "Already Ended",
                    $"That giveaway has already ended — use `{Prefix}greroll` to " +
                    "draw again.");

                return;

            case GiveawayDrawResult.StillRunning:
                await ReplyNoticeAsync(
                    "Still Running",
                    $"That giveaway is still running — end it first with `{Prefix}gend`.");

                return;

            case GiveawayDrawResult.GuildUnavailable:
                await ReplyNoticeAsync(
                    "Unavailable",
                    "This server is not fully cached yet — try again in a moment.");

                return;

            default:
                await ReplyNoticeAsync(
                    "Giveaway Not Found",
                    $"That giveaway is no longer being tracked — check `{Prefix}glist`.");

                return;
        }
    }

    /// <summary>
    /// Resolves the giveaway a command should act on from an optional raw ID or jump
    /// link, falling back to the only candidate when the guild has exactly one.
    /// </summary>
    private bool TryResolveGiveaway(
        string? token,
        IReadOnlyList<Giveaway> candidates,
        string descriptor,
        out Giveaway giveaway,
        out string error)
    {
        giveaway = null!;
        error = string.Empty;

        if (!string.IsNullOrWhiteSpace(token))
        {
            if (!TryParseMessageId(token, out var messageId))
            {
                error = $"{Inline(token)} is not a giveaway ID or message link — " +
                    $"`{Prefix}glist` shows the IDs.";

                return false;
            }

            var match = candidates.FirstOrDefault(
                candidate => candidate.MessageId == messageId);

            if (match is null)
            {
                error = $"No {descriptor}giveaway with ID `{messageId}` here — " +
                    $"check `{Prefix}glist`.";

                return false;
            }

            giveaway = match;
            return true;
        }

        if (candidates.Count == 0)
        {
            error = $"No {descriptor}giveaway is being tracked in this server.";
            return false;
        }

        // One candidate means the ID is just noise, but guessing between two could
        // end the wrong giveaway, so that case has to be spelled out.
        if (candidates.Count > 1)
        {
            error = $"`{candidates.Count}` {descriptor}giveaways are here — pass the " +
                $"ID or jump link, both of which `{Prefix}glist` gives you.";

            return false;
        }

        giveaway = candidates[0];
        return true;
    }

    private static bool TryParseMessageId(string token, out ulong messageId)
    {
        if (ulong.TryParse(token, out messageId))
            return true;

        // glist hands out jump links, so a pasted link has to work as well as an ID.
        var trimmed = token.TrimEnd('/');
        var lastSlash = trimmed.LastIndexOf('/');

        return lastSlash >= 0 &&
            ulong.TryParse(trimmed[(lastSlash + 1)..], out messageId);
    }

    private static bool TryParseWinnerCount(string token, out int winnerCount)
    {
        winnerCount = 0;

        if (token.Length < 2 || char.ToLowerInvariant(token[^1]) != 'w')
            return false;

        // The `w` suffix is mandatory, otherwise a prize that starts with a number
        // would be read as a winner count.
        return int.TryParse(token[..^1], out winnerCount) && winnerCount > 0;
    }

    private static bool TryParseDuration(string token, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;

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

        duration = parsed > GiveawayService.MaxDuration
            ? GiveawayService.MaxDuration
            : parsed;

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

    private SocketGuildUser? ResolveTarget(string query)
    {
        if (MentionUtils.TryParseUser(query, out var mentionedUserId) ||
            ulong.TryParse(query, out mentionedUserId))
        {
            return Context.Guild.GetUser(mentionedUserId);
        }

        var guildUser = Context.Guild.Users.FirstOrDefault(user =>
            user.Username.Equals(query, StringComparison.OrdinalIgnoreCase) ||
            user.DisplayName.Equals(query, StringComparison.OrdinalIgnoreCase));

        guildUser ??= Context.Guild.Users.FirstOrDefault(user =>
            user.Username.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            user.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase));

        return guildUser;
    }

    private async Task DeleteInvocationAsync()
    {
        try
        {
            await Context.Message.DeleteAsync();
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Giveaway Cleanup Error] {exception.Message}");
        }
    }

    private Task DmNoticeAsync(string title, string message)
    {
        return DmComponentsAsync(_builder.BuildNotice(title, message));
    }

    private async Task DmComponentsAsync(MessageComponent components)
    {
        try
        {
            var channel = await Context.User.CreateDMChannelAsync();

            await channel.SendMessageAsync(
                allowedMentions: AllowedMentions.None,
                components: components);
        }
        catch (Exception exception)
        {
            // DMs closed only costs the confirmation; the rig itself is already saved.
            Console.WriteLine($"[Giveaway DM Error] {exception.Message}");
        }
    }

    private async Task<bool> EnsureGuildAsync()
    {
        if (Context.Guild is not null)
            return true;

        await ReplyNoticeAsync("Server Only", "This command can only be used in a server.");
        return false;
    }

    private async Task<bool> EnsureAllowedAsync()
    {
        if (!await EnsureGuildAsync())
            return false;

        if (Context.User is not SocketGuildUser user ||
            !(user.GuildPermissions.ManageGuild || user.GuildPermissions.Administrator))
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "You need `Manage Server` or `Administrator` permission to manage " +
                "giveaways.");

            return false;
        }

        return true;
    }

    private string Prefix => _prefixService.GetPrefix(Context.Guild?.Id);

    private static string[] Split(string? input)
    {
        return string.IsNullOrWhiteSpace(input)
            ? Array.Empty<string>()
            : input.Trim().Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private Task ReplyComponentsAsync(MessageComponent components)
    {
        return ReplyAsync(allowedMentions: AllowedMentions.None, components: components);
    }

    private Task ReplyResultAsync(string title, string message, bool persisted)
    {
        return ReplyComponentsAsync(_builder.BuildResult(title, message, persisted));
    }

    private Task ReplyNoticeAsync(string title, string message)
    {
        return ReplyComponentsAsync(_builder.BuildNotice(title, message));
    }

    private static string Inline(string value)
    {
        return $"`{value.Replace("`", "'")}`";
    }
}
