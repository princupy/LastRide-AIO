using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LastRide.Builders;

namespace LastRide.Modules;

[Name("Utility")]
public sealed class RoleInfoModule : ModuleBase<SocketCommandContext>
{
    private readonly RoleInfoComponentBuilder _builder;

    public RoleInfoModule(RoleInfoComponentBuilder builder)
    {
        _builder = builder;
    }

    [Command("roleinfo")]
    [Alias("ri")]
    [Summary("Shows detailed information about a server role.")]
    public async Task RoleInfoAsync([Remainder] string? query = null)
    {
        if (Context.Guild is null)
        {
            await ReplyAsync("This command can only be used in a server.");
            return;
        }

        if (!TryResolveRole(Context.Guild, query, out var role))
        {
            await ReplyAsync(
                allowedMentions: AllowedMentions.None,
                components: _builder.BuildMissingRole());
            return;
        }

        await ReplyAsync(
            allowedMentions: AllowedMentions.None,
            components: _builder.Build(
                role,
                Context.User.Id));
    }

    private static bool TryResolveRole(
        SocketGuild guild,
        string? query,
        out SocketRole role)
    {
        role = null!;

        if (string.IsNullOrWhiteSpace(query))
            return false;

        query = query.Trim();

        if (MentionUtils.TryParseRole(query, out var mentionedRoleId) ||
            ulong.TryParse(query, out mentionedRoleId))
        {
            var resolvedRole = guild.GetRole(mentionedRoleId);

            if (resolvedRole is null)
                return false;

            role = resolvedRole;
            return true;
        }

        var exactRole = guild.Roles.FirstOrDefault(candidate =>
            candidate.Name.Equals(
                query,
                StringComparison.OrdinalIgnoreCase));

        if (exactRole is not null)
        {
            role = exactRole;
            return true;
        }

        var partialRole = guild.Roles.FirstOrDefault(candidate =>
            candidate.Name.Contains(
                query,
                StringComparison.OrdinalIgnoreCase));

        if (partialRole is null)
            return false;

        role = partialRole;
        return true;
    }
}
