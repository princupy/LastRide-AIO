using Microsoft.Extensions.Logging;

namespace LastRide.Core;

/// <summary>
/// The whole logging stack for Lavalink4NET. The bot has no generic host and no
/// logger, so rather than pull in the framework console provider — which would
/// print category names, event ids and scopes and bury the startup lines — this
/// provider mirrors <see cref="BotRunner"/>'s own format and stays silent below
/// Warning.
/// </summary>
/// <remarks>
/// The level floor is what keeps the console clean during an outage: Lavalink's
/// socket logs the <em>first</em> connection failure at Warning and every repeat
/// attempt plus every wait notice at Debug, so a dead node costs exactly one line
/// and then nothing until it comes back.
/// </remarks>
public sealed class LavalinkConsoleLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new LavalinkConsoleLogger(ShortenCategory(categoryName));
    }

    public void Dispose()
    {
    }

    /// <summary>
    /// Turns "Lavalink4NET.Socket.LavalinkSocket" into "Lavalink" so the line reads
    /// like every other bracketed startup line instead of a namespace dump.
    /// </summary>
    private static string ShortenCategory(string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            return "Lavalink";

        if (categoryName.StartsWith("Lavalink4NET", StringComparison.Ordinal))
            return "Lavalink";

        var separatorIndex = categoryName.LastIndexOf('.');

        return separatorIndex < 0 || separatorIndex == categoryName.Length - 1
            ? categoryName
            : categoryName[(separatorIndex + 1)..];
    }
}

/// <summary>
/// Writes one line per record in <see cref="BotRunner"/>'s shape:
/// <c>[HH:mm:ss] [Warning] [Lavalink] message</c>.
/// </summary>
internal sealed class LavalinkConsoleLogger : ILogger
{
    /// <summary>
    /// A record can be a message plus an exception, and Lavalink reconnects from a
    /// background task, so the two writes are held together to stop interleaving.
    /// </summary>
    private static readonly object WriteLock = new();

    private readonly string _source;

    public LavalinkConsoleLogger(string source)
    {
        _source = source;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= LogLevel.Warning && logLevel != LogLevel.None;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);

        if (string.IsNullOrWhiteSpace(message) && exception is null)
            return;

        var severity = logLevel switch
        {
            LogLevel.Critical => "Critical",
            LogLevel.Error => "Error",
            _ => "Warning"
        };

        lock (WriteLock)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss}] " +
                $"[{severity}] " +
                $"[{_source}] {message}");

            // Only the message: a full stack trace from a public node that simply
            // refused a socket is noise, and the message already names the node.
            if (exception is not null)
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{severity}] [{_source}] {exception.Message}");
        }
    }
}
