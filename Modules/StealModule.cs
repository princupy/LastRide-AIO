using System.Text.RegularExpressions;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Core;
using LastRide.Services;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed partial class StealModule : ModuleBase<SocketCommandContext>
{
    private const int MaxItemsPerRun = 10;
    private const int MinNameLength = 2;
    private const int MaxEmojiNameLength = 32;
    private const int MaxStickerNameLength = 30;

    private readonly StealComponentBuilder _builder;
    private readonly StealService _stealService;

    public StealModule(
        StealComponentBuilder builder,
        StealService stealService)
    {
        _builder = builder;
        _stealService = stealService;
    }

    [Command("steal")]
    [Alias("addemoji", "addsticker")]
    [Summary("Adds emojis or stickers from a replied message to this server.")]
    public async Task StealAsync([Remainder] string? input = null)
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
                "I need `Manage Emojis and Stickers` or `Administrator` permission to add emojis and stickers.");
            return;
        }

        var referenced = Context.Message.ReferencedMessage;
        var emoteSource = referenced?.Content ?? input;

        var emotes = ExtractEmotes(Context.Guild, emoteSource);
        var stickers = ExtractStickers(Context.Guild, referenced);

        if (emotes.Length == 0 && stickers.Length == 0)
        {
            await ReplyNoticeAsync(
                "Nothing To Steal",
                "Reply to a message that has a custom emoji or sticker, then use `?steal`.");
            return;
        }

        var addedEmojis = new List<string>();
        var addedStickers = new List<string>();
        var failedCount = 0;
        string? thumbnailUrl = null;
        string? failureReason = null;

        foreach (var emote in emotes.Take(MaxItemsPerRun))
        {
            var display = await TryAddEmoteAsync(emote, moderator.Username);

            if (display is null)
            {
                failedCount++;
                continue;
            }

            addedEmojis.Add(display);
            thumbnailUrl ??= emote.Url;
        }

        var remainingSlots = MaxItemsPerRun - emotes.Take(MaxItemsPerRun).Count();

        foreach (var sticker in stickers.Take(Math.Max(0, remainingSlots)))
        {
            var result = await TryAddStickerAsync(sticker, moderator.Username);

            if (result.Name is null)
            {
                failedCount++;
                failureReason ??= result.Error;
                continue;
            }

            addedStickers.Add(result.Name);
            thumbnailUrl ??= sticker.Url;
        }

        if (addedEmojis.Count == 0 && addedStickers.Count == 0)
        {
            await ReplyNoticeAsync(
                "Steal Failed",
                failureReason is null
                    ? "I could not add any of those. They may be too large, already added, or my slots are full."
                    : $"I could not add that. Reason: `{failureReason}`");
            return;
        }

        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildResult(
                addedEmojis,
                addedStickers,
                failedCount,
                thumbnailUrl,
                moderator.Id));
    }

    private async Task<string?> TryAddEmoteAsync(Emote emote, string moderatorName)
    {
        try
        {
            var bytes = await _stealService.DownloadAsync(
                emote.Url,
                StealService.MaxEmojiBytes);

            if (bytes is null)
                return null;

            using var stream = new MemoryStream(bytes);

            var created = await Context.Guild.CreateEmoteAsync(
                SanitizeName(emote.Name, MaxEmojiNameLength, "emoji"),
                new Image(stream),
                options: new RequestOptions
                {
                    AuditLogReason = $"Emoji stolen by {moderatorName}"
                });

            return created.ToString();
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Steal Emote Error] {emote.Name}: {DiscordFailure.Summarize(exception)}");
            return null;
        }
    }

    private async Task<StickerAddResult> TryAddStickerAsync(
        StealableSticker sticker,
        string moderatorName)
    {
        try
        {
            var bytes = await _stealService.DownloadAsync(
                sticker.Url,
                StealService.MaxStickerBytes);

            if (bytes is null)
            {
                return new StickerAddResult(
                    null,
                    "download failed or file too large (max 512 KB)");
            }

            using var stream = new MemoryStream(bytes);

            var name = SanitizeName(sticker.Name, MaxStickerNameLength, "sticker");

            var created = await Context.Guild.CreateStickerAsync(
                name,
                stream,
                $"sticker.{sticker.Extension}",
                new[] { "sticker" },
                $"Stolen sticker: {name}",
                new RequestOptions
                {
                    AuditLogReason = $"Sticker stolen by {moderatorName}"
                });

            return new StickerAddResult(created.Name, null);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Steal Sticker Error] {sticker.Name}: {DiscordFailure.Format(exception)}");
            return new StickerAddResult(null, exception.Message);
        }
    }

    private static Emote[] ExtractEmotes(SocketGuild guild, string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return Array.Empty<Emote>();

        var seen = new HashSet<ulong>();
        var emotes = new List<Emote>();

        foreach (Match match in EmoteRegex().Matches(source))
        {
            if (!Emote.TryParse(match.Value, out var emote))
                continue;

            if (!seen.Add(emote.Id))
                continue;

            // Skip emojis this server already has.
            if (guild.Emotes.Any(existing => existing.Id == emote.Id))
                continue;

            emotes.Add(emote);
        }

        return emotes.ToArray();
    }

    private static StealableSticker[] ExtractStickers(
        SocketGuild guild,
        IUserMessage? referenced)
    {
        if (referenced is null || referenced.Stickers.Count == 0)
            return Array.Empty<StealableSticker>();

        var seen = new HashSet<ulong>();
        var stickers = new List<StealableSticker>();

        foreach (var sticker in referenced.Stickers)
        {
            // Lottie stickers are JSON animations and cannot be re-uploaded.
            // GIF stickers are served from the media proxy, not the CDN.
            (string Host, string Extension)? target = sticker.Format switch
            {
                StickerFormatType.Png => ("cdn.discordapp.com", "png"),
                StickerFormatType.Apng => ("cdn.discordapp.com", "png"),
                StickerFormatType.Gif => ("media.discordapp.net", "gif"),
                _ => null
            };

            if (target is null)
                continue;

            if (!seen.Add(sticker.Id))
                continue;

            var url =
                $"https://{target.Value.Host}/stickers/{sticker.Id}.{target.Value.Extension}";

            stickers.Add(new StealableSticker(
                sticker.Id,
                sticker.Name,
                url,
                target.Value.Extension));
        }

        return stickers.ToArray();
    }

    private static string SanitizeName(string name, int maxLength, string fallback)
    {
        var cleaned = new string(
            name.Select(character =>
                    char.IsLetterOrDigit(character) || character == '_'
                        ? character
                        : '_')
                .ToArray());

        cleaned = cleaned.Trim('_');

        if (cleaned.Length < MinNameLength)
            cleaned = fallback;

        if (cleaned.Length > maxLength)
            cleaned = cleaned[..maxLength];

        return cleaned;
    }

    private async Task ReplyNoticeAsync(string title, string message)
    {
        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildNotice(title, message));
    }

    private static bool HasManageExpressions(GuildPermissions permissions)
    {
        return permissions.ManageEmojisAndStickers || permissions.Administrator;
    }

    [GeneratedRegex(@"<a?:\w{2,32}:\d+>")]
    private static partial Regex EmoteRegex();

    private readonly record struct StealableSticker(
        ulong Id,
        string Name,
        string Url,
        string Extension);

    private readonly record struct StickerAddResult(
        string? Name,
        string? Error);
}
