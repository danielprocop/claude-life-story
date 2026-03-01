using System.Collections.Concurrent;
using System.Threading.Channels;
using DiarioIntelligente.Core.Interfaces;

namespace DiarioIntelligente.API.Services;

public sealed class FeedbackReplayQueue : IFeedbackReplayScheduler
{
    private readonly Channel<Guid> _channel;
    private readonly ConcurrentDictionary<Guid, byte> _scheduledJobs = new();

    public FeedbackReplayQueue()
    {
        _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
        {
            SingleReader = true
        });
    }

    public async ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (_scheduledJobs.TryAdd(jobId, 0))
            await _channel.Writer.WriteAsync(jobId, cancellationToken);
    }

    public async ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
    {
        var jobId = await _channel.Reader.ReadAsync(cancellationToken);
        _scheduledJobs.TryRemove(jobId, out _);
        return jobId;
    }
}
