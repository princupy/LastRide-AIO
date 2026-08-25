using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;
using LastRide.Services;

namespace LastRide.Modules;

[Name("Moderation")]
public sealed class SetPrefixModule : ModuleBase<SocketCommandContext>
{
    private readonly SetPrefixComponentBuilder _builder;
    private readonly PrefixService _prefixService;

    public SetPrefixModule(
        SetPrefixComponentBuilder builder,
        PrefixService prefixService)
    {
        _builder = builder;
        _prefixService = prefixService;
    }

    [Command("setprefix")]
    [Alias("prefix", "changeprefix")]
    [Summary("Sets this server's custom command prefix.")]
    public async Task SetPrefixAsync([Remainder] string? input = null)
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
            !HasManagePermission(moderator.GuildPermissions))
        {
            await ReplyNoticeAsync(
                "Missing Permission",
                "You need `Manage Server` or `Administrator` permission to change the prefix.");
            return;
        }

        var value = input?.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            await ReplyNoticeAsync(
                "Invalid Usage",
                $"Usage: `{_prefixService.GetPrefix(Context.Guild.Id)}setprefix <new prefix>` or `{_prefixService.GetPrefix(Context.Guild.Id)}setprefix reset`.");
            return;
        }

        if (value.Equals("reset", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            var persisted = await _prefixService.ResetPrefixAsync(Context.Guild.Id);

            await ReplyAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildUpdated(
                    _prefixService.DefaultPrefix,
                    isDefault: true,
                    isPersistent: persisted,
                    moderator.Id,
                    Context.Client.CurrentUser.GetDisplayAvatarUrl(size: 256),
                    Context.Client.CurrentUser.Username));
            return;
        }

        if (value.Any(char.IsWhiteSpace))
        {
            await ReplyNoticeAsync(
                "Invalid Prefix",
                "The prefix cannot contain spaces.");
            return;
        }

        if (value.Length > PrefixService.MaxPrefixLength)
        {
            await ReplyNoticeAsync(
                "Invalid Prefix",
                $"The prefix can be at most `{PrefixService.MaxPrefixLength}` characters long.");
            return;
        }

        var saved = await _prefixService.SetPrefixAsync(Context.Guild.Id, value);

        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildUpdated(
                value,
                isDefault: false,
                isPersistent: saved,
                moderator.Id,
                Context.Client.CurrentUser.GetDisplayAvatarUrl(size: 256),
                Context.Client.CurrentUser.Username));
    }

    private async Task ReplyNoticeAsync(string title, string message)
    {
        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.BuildNotice(title, message));
    }

    private static bool HasManagePermission(GuildPermissions permissions)
    {
        return permissions.ManageGuild || permissions.Administrator;
    }
}
