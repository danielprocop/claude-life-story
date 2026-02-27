using System.Threading.Channels;

namespace DiarioIntelligente.API.Services;

public class EntryProcessingQueue
{
    private readonly Channel<EntryProcessingJob> _channel;

    public EntryProcessingQueue()
    {
        _channel = Channel.CreateUnbounded<EntryProcessingJob>(new UnboundedChannelOptions
        {
            SingleReader = true
        });
    }

    public async ValueTask EnqueueAsync(EntryProcessingJob job, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(job, ct);
    }

    public async ValueTask<EntryProcessingJob> DequeueAsync(CancellationToken ct)
    {
        return await _channel.Reader.ReadAsync(ct);
    }
}

public record EntryProcessingJob(Guid EntryId, Guid UserId, string Content);
