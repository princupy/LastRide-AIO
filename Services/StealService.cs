namespace LastRide.Services;

public sealed class StealService
{
    public const long MaxEmojiBytes = 256 * 1024;
    public const long MaxStickerBytes = 512 * 1024;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _client = new()
    {
        Timeout = RequestTimeout
    };

    public async Task<byte[]?> DownloadAsync(string url, long maxBytes)
    {
        try
        {
            var bytes = await _client.GetByteArrayAsync(url);

            if (bytes.Length == 0 || bytes.Length > maxBytes)
                return null;

            return bytes;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Steal Download Error] {exception.Message}");
            return null;
        }
    }
}
