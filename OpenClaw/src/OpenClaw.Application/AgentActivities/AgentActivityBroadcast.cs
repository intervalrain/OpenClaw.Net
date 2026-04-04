using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace OpenClaw.Application.AgentActivities;

/// <summary>
/// In-process fan-out broadcast for agent activity events.
/// Each SSE subscriber gets its own bounded channel.
/// </summary>
public sealed class AgentActivityBroadcast : IAgentActivityBroadcast
{
    private readonly List<Channel<AgentActivityEvent>> _subscribers = [];
    private readonly Lock _lock = new();

    public ValueTask PublishAsync(AgentActivityEvent evt, CancellationToken ct = default)
    {
        lock (_lock)
        {
            for (int i = _subscribers.Count - 1; i >= 0; i--)
            {
                if (!_subscribers[i].Writer.TryWrite(evt))
                {
                    // Subscriber is full or completed — remove it
                    _subscribers.RemoveAt(i);
                }
            }
        }

        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<AgentActivityEvent> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateBounded<AgentActivityEvent>(
            new BoundedChannelOptions(256)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });

        lock (_lock)
        {
            _subscribers.Add(channel);
        }

        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(ct))
            {
                yield return evt;
            }
        }
        finally
        {
            channel.Writer.TryComplete();
            lock (_lock)
            {
                _subscribers.Remove(channel);
            }
        }
    }
}
