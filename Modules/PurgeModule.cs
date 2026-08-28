using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Core;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed class PurgeModule : ModuleBase<SocketCommandContext>
{
    private const int MaxCount = 100;
    private static readonly TimeSpan BulkDeleteLimit = TimeSpan.FromDays(14);
    private static readonly TimeSpan AutoDeleteDelay = TimeSpan.FromSeconds(5);
    private readonly PurgeComponentBuilder _builder;

    public PurgeModule(PurgeComponentBuilder builder)
    {
        _builder = builder;
    }

    [Command("purge")]
    [Alias("clear", "clean")]
    [Summary("Deletes recent messages, optionally filtered by humans, bots, or media.")]
    public async Task PurgeAsync([Remainder] string? input = null)
    {
        if (Context.Guild is null)
        {
            await ReplyNoticeAsync(
                "Server Only",
                "This command can only be used in a server.");
            return;
        }

        if (Context.Channel is not SocketTextChannel channel ||
            Context.Channel is SocketThreadChannel)
        {
            await ReplyNoticeAsync(
                "Unsupported Channel",
                "I can only purge messages in standard text channels.");
            return;
        }

        var moderator = Context.User as SocketGuildUser;

        if (moderator is null ||
            !HasManageMessages(moderator.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "You need `Manage Messages` or `Administrator` permission to use this command.");
            return;
        }

        if (!HasManageMessages(Context.Guild.CurrentUser.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Bot Permission",
                "I need `Manage Messages` or `Administrator` permission to purge messages.");
            return;
        }

        var parsed = ParseInput(input);

        if (parsed is null)
        {
            await ReplyNoticeAsync(
                "Invalid Usage",
                "Usage: `?purge 20`, `?purge @user 20`, `?purge humans 20`, `?purge bots 20`, or `?purge media 20`.");
            return;
        }

        var (filter, targetUserId, count) = parsed.Value;

        var fetched = await channel
            .GetMessagesAsync(MaxCount)
            .FlattenAsync();

        var matching = fetched
            .Where(message => message.Id != Context.Message.Id)
            .Where(message => MatchesFilter(message, filter, targetUserId))
            .Take(count)
            .ToArray();

        if (matching.Length == 0)
        {
            await ReplyNoticeAsync(
                "Nothing To Purge",
                "No matching messages were found in the recent history.");
            return;
        }

        var threshold = DateTimeOffset.UtcNow - BulkDeleteLimit;
        var deletable = matching
            .Where(message => message.Timestamp > threshold)
            .ToArray();
        var skippedOldCount = matching.Length - deletable.Length;

        try
        {
            if (deletable.Length == 1)
            {
                await deletable[0].DeleteAsync();
            }
            else if (deletable.Length > 1)
            {
                await channel.DeleteMessagesAsync(deletable);
            }

            await TryDeleteCommandMessageAsync();

            var reply = await ReplyAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildSuccess(
                    deletable.Length,
                    FilterText(filter),
                    targetUserId,
                    skippedOldCount,
                    moderator.Id));

            await DeleteAfterDelayAsync(reply);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Purge Error] {DiscordFailure.Format(exception)}");

            await ReplyNoticeAsync(
                "Purge Failed",
                DiscordFailure.Describe(
                    exception,
                    "I could not delete those messages. Check my permissions."));
        }
    }

    private async Task TryDeleteCommandMessageAsync()
    {
        try
        {
            await Context.Message.DeleteAsync();
        }
        catch
        {
            // Command message may already be gone; ignore.
        }
    }

    private static async Task DeleteAfterDelayAsync(IUserMessage message)
    {
        await Task.Delay(AutoDeleteDelay);

        try
        {
            await message.DeleteAsync();
        }
        catch
        {
            // Reply may already be gone; ignore.
        }
    }

    private async Task ReplyNoticeAsync(string title, string message)
    {
        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildNotice(title, message));
    }

    private static bool MatchesFilter(
        IMessage message,
        PurgeFilter filter,
        ulong targetUserId)
    {
        if (targetUserId != 0 && message.Author.Id != targetUserId)
            return false;

        return filter switch
        {
            PurgeFilter.Humans => !message.Author.IsBot && !message.Author.IsWebhook,
            PurgeFilter.Bots => message.Author.IsBot || message.Author.IsWebhook,
            PurgeFilter.Media => message.Attachments.Count > 0 || message.Embeds.Count > 0,
            _ => true
        };
    }

    private static bool HasManageMessages(GuildPermissions permissions)
    {
        return permissions.ManageMessages || permissions.Administrator;
    }

    private static ParsedPurge? ParseInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var tokens = input
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var filter = PurgeFilter.All;
        ulong targetUserId = 0;
        int? count = null;

        foreach (var token in tokens)
        {
            if (MentionUtils.TryParseUser(token, out var mentionedId))
            {
                if (targetUserId != 0)
                    return null;

                targetUserId = mentionedId;
                continue;
            }

            if (int.TryParse(token, out var value))
            {
                if (count is not null)
                    return null;

                count = value;
                continue;
            }

            if (ulong.TryParse(token, out var rawId) && rawId > int.MaxValue)
            {
                if (targetUserId != 0)
                    return null;

                targetUserId = rawId;
                continue;
            }

            if (TryParseFilter(token, out var parsedFilter))
            {
                filter = parsedFilter;
                continue;
            }

            return null;
        }

        if (count is not { } finalCount || finalCount <= 0)
            return null;

        return new ParsedPurge(
            filter,
            targetUserId,
            Math.Min(finalCount, MaxCount));
    }

    private static bool TryParseFilter(string token, out PurgeFilter filter)
    {
        switch (token.ToLowerInvariant())
        {
            case "human":
            case "humans":
            case "users":
                filter = PurgeFilter.Humans;
                return true;
            case "bot":
            case "bots":
                filter = PurgeFilter.Bots;
                return true;
            case "media":
            case "image":
            case "images":
            case "files":
                filter = PurgeFilter.Media;
                return true;
            case "all":
                filter = PurgeFilter.All;
                return true;
            default:
                filter = PurgeFilter.All;
                return false;
        }
    }

    private static string FilterText(PurgeFilter filter)
    {
        return filter switch
        {
            PurgeFilter.Humans => "Humans",
            PurgeFilter.Bots => "Bots",
            PurgeFilter.Media => "Media",
            _ => "All"
        };
    }

    private enum PurgeFilter
    {
        All,
        Humans,
        Bots,
        Media
    }

    private readonly record struct ParsedPurge(
        PurgeFilter Filter,
        ulong TargetUserId,
        int Count);
}
