using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Maestro.Api.Services;

/// <summary>
/// Typed event published on the in-process bus. Payload is serialized as JSON
/// before being sent over SSE.
/// </summary>
public sealed record AgentEvent(string Type, object Payload);

/// <summary>
/// In-process pub/sub keyed by workspace id. Each <see cref="Subscribe"/> call
/// returns its own bounded channel; if a slow consumer lags, the oldest event
/// is dropped instead of stalling the whole pipeline.
/// </summary>
public sealed class EventBus
{
    private readonly ConcurrentDictionary<string, List<ChannelWriter<AgentEvent>>> _channels = new();

    public void Publish(string workspaceId, AgentEvent ev)
    {
        if (!_channels.TryGetValue(workspaceId, out var writers)) return;
        // Snapshot to release the lock quickly.
        ChannelWriter<AgentEvent>[] snapshot;
        lock (writers)
        {
            snapshot = [.. writers];
        }
        foreach (var w in snapshot)
        {
            w.TryWrite(ev);
        }
    }

    public async IAsyncEnumerable<AgentEvent> Subscribe(
        string workspaceId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateBounded<AgentEvent>(new BoundedChannelOptions(4096)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        var list = _channels.GetOrAdd(workspaceId, _ => []);
        lock (list) list.Add(channel.Writer);

        try
        {
            await foreach (var ev in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return ev;
            }
        }
        finally
        {
            lock (list) list.Remove(channel.Writer);
            channel.Writer.TryComplete();
        }
    }
}
