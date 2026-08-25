using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using LastRide.Builders;

namespace LastRide.Modules;

[Name("Utility")]
public sealed class ProfileModule : ModuleBase<SocketCommandContext>
{
    private readonly ProfileComponentBuilder _builder;

    public ProfileModule(ProfileComponentBuilder builder)
    {
        _builder = builder;
    }

    [Command("avatar")]
    [Alias("av", "pfp")]
    [Summary("Shows a user's global or server profile picture.")]
    public Task AvatarAsync(SocketUser? user = null)
    {
        var targetUser = user ?? Context.User;
        var guildUser = GetGuildUser(targetUser.Id);
        var guildId = Context.Guild?.Id ?? 0;

        var components = _builder.BuildAvatar(
            targetUser,
            guildUser,
            Context.User.Id,
            guildId,
            AvatarView.Global);

        return ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: components);
    }

    [Command("banner")]
    [Alias("bn")]
    [Summary("Shows a user's banner when one is available.")]
    public async Task BannerAsync(SocketUser? user = null)
    {
        var targetUser = user ?? Context.User;
        var guildUser = GetGuildUser(targetUser.Id);
        var bannerUrl = await GetBannerUrlAsync(targetUser, guildUser);

        var components = _builder.BuildBanner(targetUser, bannerUrl);

        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: components);
    }

    private SocketGuildUser? GetGuildUser(ulong userId)
    {
        return Context.Guild?.GetUser(userId);
    }

    private async Task<string?> GetBannerUrlAsync(
        SocketUser targetUser,
        SocketGuildUser? guildUser)
    {
        var restUser = await Context.Client.Rest.GetUserAsync(targetUser.Id);

        var globalBannerUrl = restUser?.GetBannerUrl(size: 2048);

        if (!string.IsNullOrWhiteSpace(globalBannerUrl))
            return globalBannerUrl;

        return guildUser?.GetGuildBannerUrl(size: 2048);
    }
}
