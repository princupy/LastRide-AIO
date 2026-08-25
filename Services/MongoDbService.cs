using System.Diagnostics;
using LastRide.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LastRide.Services;

public sealed class MongoDbService
{
    private readonly string? _connectionString;
    private readonly Lazy<IMongoClient?> _client;

    public MongoDbService(BotOptions options)
    {
        _connectionString = options.MongoConnectionString;
        _client = new Lazy<IMongoClient?>(CreateClient);
    }

    public async Task<string> GetStatusAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            return "Not configured";

        var result = await PingAsync();

        return result.IsConnected
            ? $"Connected ({result.LatencyMilliseconds} ms)"
            : result.Status;
    }

    public async Task<string> GetLatencyAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            return "Not configured";

        var result = await PingAsync();

        return result.IsConnected
            ? $"{result.LatencyMilliseconds}ms"
            : result.Status;
    }

    public IMongoCollection<T>? GetCollection<T>(
        string databaseName,
        string collectionName)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            return null;

        try
        {
            var client = _client.Value;

            if (client is null)
                return null;

            return client
                .GetDatabase(databaseName)
                .GetCollection<T>(collectionName);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Mongo Collection Error] {exception.Message}");
            return null;
        }
    }

    private async Task<DatabasePingResult> PingAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var timeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(5));

            var client = _client.Value;

            if (client is null)
                return DatabasePingResult.Failed("Invalid configuration");

            var command = new BsonDocumentCommand<BsonDocument>(
                new BsonDocument("ping", 1));

            await client
                .GetDatabase("admin")
                .RunCommandAsync(command, cancellationToken: timeout.Token);

            stopwatch.Stop();

            return DatabasePingResult.Connected(
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            return DatabasePingResult.Failed("Timeout");
        }
        catch (MongoException)
        {
            return DatabasePingResult.Failed("Disconnected");
        }
        catch (FormatException)
        {
            return DatabasePingResult.Failed("Invalid configuration");
        }
        catch (ArgumentException)
        {
            return DatabasePingResult.Failed("Invalid configuration");
        }
    }

    private IMongoClient? CreateClient()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            return null;

        var settings = MongoClientSettings.FromConnectionString(
            _connectionString);

        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);

        return new MongoClient(settings);
    }

    private sealed record DatabasePingResult(
        bool IsConnected,
        long LatencyMilliseconds,
        string Status)
    {
        public static DatabasePingResult Connected(long latencyMilliseconds)
        {
            return new DatabasePingResult(
                true,
                latencyMilliseconds,
                "Connected");
        }

        public static DatabasePingResult Failed(string status)
        {
            return new DatabasePingResult(false, 0, status);
        }
    }
}
