using Discord;
using Discord.Net;

namespace LastRide.Core;

/// <summary>
/// Explains a rejected Discord request in terms the person running the command can act on.
/// Two very different problems reach a catch block looking identical — the bot genuinely
/// lacking a permission, and a server that gates moderation behind two-factor
/// authentication — and only one of them is fixed by touching roles, so a blanket
/// "check my permissions" sends people hunting for something that was never wrong.
/// </summary>
internal static class DiscordFailure
{
    /// <summary>
    /// What a guild with "2FA Requirement for Moderation" enabled actually needs. The gate
    /// sits on the account that owns the bot application — not on the server owner, not on
    /// the bot's role, and not on its permissions — so nothing inside the server clears it.
    /// </summary>
    private const string TwoFactorMessage =
        "This server has **2FA Requirement for Moderation** turned on, and the Discord " +
        "account that owns me does not have two-factor authentication enabled. Every " +
        "moderation action is blocked until it does. Enable 2FA on that account — the app " +
        "owner in the Developer Portal, or the team owner if the app belongs to a team — " +
        "and this works straight away. My permissions and role position are fine.";

    /// <summary>The console form of the same cause, kept to a single readable line.</summary>
    private const string TwoFactorLog =
        "Discord error 60003 — this server requires 2FA for moderation and the bot " +
        "application owner's account does not have it enabled. Not a permission problem " +
        "and not a bug; enable 2FA on the owner account.";

    /// <summary>
    /// True when Discord answered with error 60003 rather than an ordinary permission
    /// failure, however deeply the rejection ended up wrapped.
    /// </summary>
    public static bool IsTwoFactorRequired(Exception? exception)
    {
        return exception switch
        {
            null => false,
            HttpException http => http.DiscordCode == DiscordErrorCode.Requires2FA,
            AggregateException aggregate =>
                aggregate.InnerExceptions.Any(IsTwoFactorRequired),
            _ => IsTwoFactorRequired(exception.InnerException)
        };
    }

    /// <summary>
    /// The text to show a member: the 2FA explanation when that is the real cause,
    /// otherwise whatever the call site already wanted to say.
    /// </summary>
    public static string Describe(Exception? exception, string fallback)
    {
        return IsTwoFactorRequired(exception) ? TwoFactorMessage : fallback;
    }

    /// <summary>
    /// The same explanation for call sites that only have an outcome flag to go on, because
    /// the request failed inside a service that already swallowed the exception.
    /// </summary>
    public static string TwoFactorNotice()
    {
        return TwoFactorMessage;
    }

    /// <summary>
    /// The console line for a site that normally prints a full stack trace. A trace earns
    /// its space for a defect, but 60003 is a server setting, so it gets one line instead
    /// of a screen of frames every time somebody runs a moderation command.
    /// </summary>
    public static string Format(Exception exception)
    {
        return IsTwoFactorRequired(exception) ? TwoFactorLog : exception.ToString();
    }

    /// <summary>
    /// The console line for a site that normally prints only the message. Discord's own
    /// wording for 60003 — "Two factor is required for this operation" — never says whose
    /// two factor, which is the one detail needed to fix it.
    /// </summary>
    public static string Summarize(Exception exception)
    {
        return IsTwoFactorRequired(exception) ? TwoFactorLog : exception.Message;
    }
}
