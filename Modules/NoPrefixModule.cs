using Discord;
using Discord.Commands;
using LastRide.Builders;
using LastRide.Core;
using LastRide.Services;

namespace LastRide.Modules;

[Name("NoPrefix")]
public sealed class NoPrefixModule : ModuleBase<SocketCommandContext>
{
    private readonly NoPrefixService _service;
    private readonly NoPrefixComponentBuilder _builder;
    private readonly PrefixService _prefixService;

    public NoPrefixModule(
        NoPrefixService service,
        NoPrefixComponentBuilder builder,
        PrefixService prefixService)
    {
        _service = service;
        _builder = builder;
        _prefixService = prefixService;
    }

    [Command("nop")]
    [Alias("noprefix")]
    [Summary("Manage no-prefix access.")]
    [Remarks(HelpComponentBuilder.HiddenCommandRemark)]
    public async Task NoPrefixAsync([Remainder] string? input = null)
    {
        // Nobody but the owner gets any reply at all. Even a "missing permission"
        // card would tell the channel this command exists, and it is meant to stay
        // completely invisible.
        if (!_service.IsOwner(Context.User.Id))
            return;

        var parts = Split(input);
        var action = parts.Length > 0 ? parts[0].ToLowerInvariant() : string.Empty;
        var query = string.Join(' ', parts.Skip(1));

        // The invocation goes first on every path, so no trace of the command is
        // left in the channel even when the arguments turn out to be wrong.
        await DeleteInvocationAsync();

        switch (action)
        {
            case "add":
            case "grant":
                await AddAsync(query);
                return;

            case "remove":
            case "revoke":
            case "delete":
                await RemoveAsync(query);
                return;

            case "list":
            case "show":
                await ListAsync();
                return;

            default:
                await ReplyUsageAsync();
                return;
        }
    }

    /// <summary>
    /// Posts the duration dropdown instead of granting immediately, so the length is
    /// picked from a fixed list rather than typed and mistyped.
    /// </summary>
    private async Task AddAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            await ReplyNoticeAsync(
                "Missing Member",
                $"Usage: {Inline($"{Prefix}nop add <@user|id>")}.");

            return;
        }

        var target = await ResolveTargetAsync(query);

        if (target is null)
        {
            await ReplyNoticeAsync(
                "Member Not Found",
                $"I could not find {Inline(query)}. Mention the member or use their user ID.");

            return;
        }

        if (target.IsBot)
        {
            await ReplyNoticeAsync(
                "Bots Not Supported",
                "I ignore every message a bot sends, so no-prefix access would do " +
                "nothing for one.");

            return;
        }

        await ReplyComponentsAsync(
            _builder.BuildDurationPrompt(target, Context.User.Id));
    }

    private async Task RemoveAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            await ReplyNoticeAsync(
                "Missing Member",
                $"Usage: {Inline($"{Prefix}nop remove <@user|id>")}.");

            return;
        }

        var target = await ResolveTargetAsync(query);

        // Someone who left every shared server can still hold a grant, so a raw id
        // that resolves to nobody is still worth revoking.
        var targetId = target?.Id
            ?? (ulong.TryParse(query.Trim(), out var rawId) ? rawId : 0);

        if (targetId == 0)
        {
            await ReplyNoticeAsync(
                "Member Not Found",
                $"I could not find {Inline(query)}. Mention the member or use their user ID.");

            return;
        }

        var outcome = await _service.RevokeAsync(targetId);

        if (outcome.Result == NoPrefixRevokeResult.NotFound)
        {
            await ReplyNoticeAsync(
                "Not Granted",
                $"<@{targetId}> does not have no-prefix access.");

            return;
        }

        await ReplyResultAsync(
            "No-Prefix Removed",
            $"<@{targetId}> needs the {Inline(Prefix)} prefix again.",
            outcome.Persisted);
    }

    private async Task ListAsync()
    {
        var entries = _service.GetAll();

        await ReplyComponentsAsync(
            _builder.BuildList(
                entries,
                ResolveUsers(entries.Select(entry => entry.UserId)),
                0,
                Context.User.Id));
    }

    private Task ReplyUsageAsync()
    {
        return ReplyNoticeAsync(
            "No-Prefix",
            $"{Inline($"{Prefix}nop add <@user|id>")} grants access • " +
            $"{Inline($"{Prefix}nop remove <@user|id>")} revokes it • " +
            $"{Inline($"{Prefix}nop list")} shows everyone who holds it.");
    }

    /// <summary>
    /// Cache-only on purpose: a page holds several members and a REST fetch each
    /// would turn one listing into a handful of HTTP calls. Anyone missing simply
    /// renders without an avatar.
    /// </summary>
    private Dictionary<ulong, IUser?> ResolveUsers(IEnumerable<ulong> userIds)
    {
        var users = new Dictionary<ulong, IUser?>();

        foreach (var userId in userIds)
        {
            if (users.ContainsKey(userId))
                continue;

            users[userId] =
                Context.Guild?.GetUser(userId) as IUser ??
                Context.Client.GetUser(userId);
        }

        return users;
    }

    /// <summary>
    /// Only an explicit reference counts — see <see cref="UserReference"/> for why a plain
    /// name is refused rather than matched.
    /// </summary>
    private async Task<IUser?> ResolveTargetAsync(string query)
    {
        if (!UserReference.TryParse(query, out var userId))
            return null;

        // A grant covers every server, so the target does not have to be a
        // member here — the id falls back to the shared cache and then to REST.
        return Context.Guild?.GetUser(userId) as IUser ??
            Context.Client.GetUser(userId) as IUser ??
            await Context.Client.Rest.GetUserAsync(userId);
    }

    private async Task DeleteInvocationAsync()
    {
        try
        {
            await Context.Message.DeleteAsync();
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[NoPrefix Cleanup Error] {DiscordFailure.Summarize(exception)}");
        }
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
