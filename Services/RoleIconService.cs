using Discord;

namespace LastRide.Services;

public sealed class RoleIconService
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private const long MaxIconBytes = 256 * 1024;

    private readonly HttpClient _client = new()
    {
        Timeout = RequestTimeout
    };

    public async Task<Image?> DownloadIconAsync(string url)
    {
        try
        {
            var bytes = await _client.GetByteArrayAsync(url);

            if (bytes.Length == 0 || bytes.Length > MaxIconBytes)
                return null;

            return new Image(new MemoryStream(bytes));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[RoleIcon Download Error] {exception.Message}");
            return null;
        }
    }
}
