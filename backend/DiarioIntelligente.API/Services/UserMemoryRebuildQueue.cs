using System.Collections.Concurrent;
using System.Threading.Channels;

namespace DiarioIntelligente.API.Services;

public sealed class UserMemoryRebuildQueue
{
    private readonly Channel<Guid> _channel;
    private readonly ConcurrentDictionary<Guid, byte> _scheduledUsers = new();

    public UserMemoryRebuildQueue()
    {
        _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
        {
            SingleReader = true
        });
    }

    public async ValueTask EnqueueAsync(Guid userId, CancellationToken ct = default)
    {
        if (_scheduledUsers.TryAdd(userId, 0))
            await _channel.Writer.WriteAsync(userId, ct);
    }

    public async ValueTask<Guid> DequeueAsync(CancellationToken ct)
    {
        var userId = await _channel.Reader.ReadAsync(ct);
        _scheduledUsers.TryRemove(userId, out _);
        return userId;
    }
}
