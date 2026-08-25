using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Models;
using LastRide.Services;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed class BanListModule : ModuleBase<SocketCommandContext>
{
    private const int MaxBansFetched = 1000;
    private const string DefaultReason = "No reason provided.";

    private readonly BanListComponentBuilder _builder;
    private readonly BanListService _banListService;

    public BanListModule(
        BanListComponentBuilder builder,
        BanListService banListService)
    {
        _builder = builder;
        _banListService = banListService;
    }

    [Command("banlist")]
    [Alias("bans")]
    [Summary("Shows every banned member with pagination and unban controls.")]
    public async Task BanListAsync()
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
            !HasBanPermission(moderator.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "You need `Ban Members` or `Administrator` permission to use this command.");
            return;
        }

        if (!HasBanPermission(Context.Guild.CurrentUser.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Bot Permission",
                "I need `Ban Members` or `Administrator` permission to view and manage bans.");
            return;
        }

        IReadOnlyList<BannedUser> bans;

        try
        {
            var fetched = await Context.Guild
                .GetBansAsync(MaxBansFetched)
                .FlattenAsync();

            bans = fetched
                .Select(ban => new BannedUser(
                    ban.User.Id,
                    ban.User.Username,
                    ban.User.GetDisplayAvatarUrl(size: 256) ??
                        ban.User.GetDefaultAvatarUrl(),
                    string.IsNullOrWhiteSpace(ban.Reason)
                        ? DefaultReason
                        : ban.Reason))
                .ToList();
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[BanList Error] {exception}");

            await ReplyNoticeAsync(
                "Ban List Failed",
                "I could not fetch the ban list. Check my permissions and try again.");
            return;
        }

        if (bans.Count == 0)
        {
            await ReplyNoticeAsync(
                "No Bans",
                "There are no banned members in this server.");
            return;
        }

        var session = _banListService.Create(
            Context.Guild.Id,
            moderator.Id,
            bans);

        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.Build(
                session.Bans,
                session.Id,
                moderator.Id,
                0));
    }

    private async Task ReplyNoticeAsync(string title, string message)
    {
        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildNotice(title, message));
    }

    private static bool HasBanPermission(GuildPermissions permissions)
    {
        return permissions.BanMembers || permissions.Administrator;
    }
}
