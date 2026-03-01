namespace DiarioIntelligente.Core.Interfaces;

public interface IFeedbackReplayScheduler
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default);
}

