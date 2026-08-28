namespace LastRide.Configuration;

/// <summary>
/// The Lavalink node the bot plays through. The operator publishes two endpoints — TLS
/// on 443 and plaintext on 80 — but they are the same physical server, so a second
/// entry buys no real failover. The TLS one is used on its own, which keeps the audio
/// stack on a single node rather than a cluster. Keeping the address here rather than
/// inline in <c>Program.cs</c> gives it one named home, exactly like
/// <see cref="BotOptions"/>.
/// </summary>
public static class LavalinkSettings
{
    public static readonly Uri BaseAddress = new("https://lavalinkv4.serenetia.com:443/");

    /// <summary>
    /// Shared credential published by the node operator, not a secret of ours. It still
    /// never reaches the console: Lavalink4NET logs request URIs, never headers, and the
    /// console provider drops everything below Warning anyway.
    /// </summary>
    public const string Passphrase = "https://seretia.link/discord";
}
