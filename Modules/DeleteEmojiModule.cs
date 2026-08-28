using System.Text.RegularExpressions;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Core;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed partial class DeleteEmojiModule : ModuleBase<SocketCommandContext>
{
    private const int MaxItemsPerRun = 10;

    private readonly DeleteEmojiComponentBuilder _builder;

    public DeleteEmojiModule(DeleteEmojiComponentBuilder builder)
    {
        _builder = builder;
    }

    [Command("deleteemoji")]
    [Alias("delemoji", "removeemoji", "deletesticker", "delsticker", "removesticker")]
    [Summary("Deletes this server's emojis or stickers from a replied message.")]
    public async Task DeleteEmojiAsync([Remainder] string? input = null)
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
            !HasManageExpressions(moderator.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "You need `Manage Emojis and Stickers` or `Administrator` permission to use this command.");
            return;
        }

        if (!HasManageExpressions(Context.Guild.CurrentUser.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Bot Permission",
                "I need `Manage Emojis and Stickers` or `Administrator` permission to delete emojis and stickers.");
            return;
        }

        var referenced = Context.Message.ReferencedMessage;
        var emoteSource = referenced?.Content ?? input;

        var emotes = ExtractGuildEmotes(Context.Guild, emoteSource);
        var stickers = ExtractGuildStickers(Context.Guild, referenced);

        if (emotes.Length == 0 && stickers.Length == 0)
        {
            await ReplyNoticeAsync(
                "Nothing To Delete",
                "Reply to a message that has one of this server's emojis or stickers, then use `?deleteemoji`.");
            return;
        }

        var deletedEmojis = new List<string>();
        var deletedStickers = new List<string>();
        var failedCount = 0;

        // The rejection itself is kept rather than its text: the reply has to be able to
        // tell a 2FA-gated server apart from an ordinary failure, and only the exception
        // carries the code that says which one it was.
        Exception? failure = null;

        foreach (var emote in emotes.Take(MaxItemsPerRun))
        {
            try
            {
                await Context.Guild.DeleteEmoteAsync(
                    emote,
                    new RequestOptions
                    {
                        AuditLogReason = $"Emoji deleted by {moderator.Username}"
                    });

                deletedEmojis.Add(emote.Name);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[DeleteEmoji Error] {emote.Name}: {DiscordFailure.Format(exception)}");
                failedCount++;
                failure ??= exception;
            }
        }

        var remainingSlots = MaxItemsPerRun - emotes.Take(MaxItemsPerRun).Count();

        foreach (var sticker in stickers.Take(Math.Max(0, remainingSlots)))
        {
            try
            {
                await Context.Guild.DeleteStickerAsync(
                    sticker,
                    new RequestOptions
                    {
                        AuditLogReason = $"Sticker deleted by {moderator.Username}"
                    });

                deletedStickers.Add(sticker.Name);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[DeleteSticker Error] {sticker.Name}: {DiscordFailure.Format(exception)}");
                failedCount++;
                failure ??= exception;
            }
        }

        if (deletedEmojis.Count == 0 && deletedStickers.Count == 0)
        {
            await ReplyNoticeAsync(
                "Delete Failed",
                failure is null
                    ? "I could not delete any of those."
                    : DiscordFailure.Describe(
                        failure,
                        $"I could not delete that. Reason: `{failure.Message}`"));
            return;
        }

        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildResult(
                deletedEmojis,
                deletedStickers,
                failedCount,
                moderator.Id));
    }

    private async Task ReplyNoticeAsync(string title, string message)
    {
        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildNotice(title, message));
    }

    private static GuildEmote[] ExtractGuildEmotes(SocketGuild guild, string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return Array.Empty<GuildEmote>();

        var seen = new HashSet<ulong>();
        var emotes = new List<GuildEmote>();

        foreach (Match match in EmoteRegex().Matches(source))
        {
            if (!Emote.TryParse(match.Value, out var parsed))
                continue;

            if (!seen.Add(parsed.Id))
                continue;

            // Only this server's own emojis can be deleted.
            var guildEmote = guild.Emotes.FirstOrDefault(
                existing => existing.Id == parsed.Id);

            if (guildEmote is null)
                continue;

            emotes.Add(guildEmote);
        }

        return emotes.ToArray();
    }

    private static SocketCustomSticker[] ExtractGuildStickers(
        SocketGuild guild,
        IUserMessage? referenced)
    {
        if (referenced is null || referenced.Stickers.Count == 0)
            return Array.Empty<SocketCustomSticker>();

        var seen = new HashSet<ulong>();
        var stickers = new List<SocketCustomSticker>();

        foreach (var stickerItem in referenced.Stickers)
        {
            if (!seen.Add(stickerItem.Id))
                continue;

            // Only this server's own stickers can be deleted.
            var guildSticker = guild.GetSticker(stickerItem.Id);

            if (guildSticker is null)
                continue;

            stickers.Add(guildSticker);
        }

        return stickers.ToArray();
    }

    private static bool HasManageExpressions(GuildPermissions permissions)
    {
        return permissions.ManageEmojisAndStickers || permissions.Administrator;
    }

    [GeneratedRegex(@"<a?:\w{2,32}:\d+>")]
    private static partial Regex EmoteRegex();
}
