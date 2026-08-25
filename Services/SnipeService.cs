using System.Collections.Concurrent;
using LastRide.Models;

namespace LastRide.Services;

public sealed class SnipeService
{
    public const int MaxStoredPerChannel = 5;

    private const int MaxContentLength = 800;
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(2);

    private readonly ConcurrentDictionary<ulong, List<SnipedMessage>> _messages = new();

    public void Store(SnipedMessage message)
    {
        var stored = _messages.GetOrAdd(
            message.ChannelId,
            _ => new List<SnipedMessage>());

        lock (stored)
        {
            stored.Insert(0, message with { Content = NormalizeContent(message.Content) });

            if (stored.Count > MaxStoredPerChannel)
            {
                stored.RemoveRange(
                    MaxStoredPerChannel,
                    stored.Count - MaxStoredPerChannel);
            }
        }
    }

    public SnipedMessage[] GetMessages(ulong channelId)
    {
        if (!_messages.TryGetValue(channelId, out var stored))
            return Array.Empty<SnipedMessage>();

        var threshold = DateTimeOffset.UtcNow - Lifetime;

        lock (stored)
        {
            stored.RemoveAll(message => message.DeletedAt < threshold);

            return stored.ToArray();
        }
    }

    public void SetDeletedBy(
        ulong channelId,
        ulong messageId,
        ulong deletedById)
    {
        if (!_messages.TryGetValue(channelId, out var stored))
            return;

        lock (stored)
        {
            var index = stored.FindIndex(
                message => message.MessageId == messageId);

            if (index < 0)
                return;

            stored[index] = stored[index] with { DeletedById = deletedById };
        }
    }

    private static string NormalizeContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "No text content.";

        content = content.Trim();

        return content.Length <= MaxContentLength
            ? content
            : content[..MaxContentLength] + "...";
    }
}
