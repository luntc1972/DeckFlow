using System.Threading;
using System.Threading.Channels;

namespace DeckFlow.Web.Services.Analytics;

/// <summary>
/// Singleton write-behind buffer for <see cref="RequestMetricEvent"/> records.
/// Wraps a bounded <see cref="System.Threading.Channels.Channel{T}"/> with <see cref="BoundedChannelFullMode.DropOldest"/>
/// semantics so that <see cref="Enqueue"/> never blocks or throws on the request hot path
/// (D-08, SC #5). When the channel is full the oldest unread event is silently discarded
/// and an atomic drop counter is incremented; the flusher reads this counter once per minute
/// and emits a single Serilog WARN (D-10).
/// </summary>
public sealed class RequestMetricsBuffer
{
    private static readonly BoundedChannelOptions Options = new(capacity: 10_000)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false,
    };

    private readonly Channel<RequestMetricEvent> _channel;
    private long _droppedCount;

    /// <summary>
    /// Initialises the buffer with a bounded channel of capacity 10 000
    /// and <see cref="BoundedChannelFullMode.DropOldest"/> overflow policy.
    /// The <c>itemDropped</c> callback increments <see cref="_droppedCount"/> atomically
    /// each time an event is evicted — no allocation per drop.
    /// </summary>
    public RequestMetricsBuffer()
    {
        _channel = Channel.CreateBounded<RequestMetricEvent>(
            Options,
            itemDropped: _ => Interlocked.Increment(ref _droppedCount));
    }

    /// <summary>
    /// Exposes the channel reader so <see cref="RequestMetricsFlusher"/> can drain events
    /// without holding a reference to the full channel.
    /// </summary>
    public ChannelReader<RequestMetricEvent> Reader => _channel.Reader;

    /// <summary>
    /// Non-blocking enqueue. Under <see cref="BoundedChannelFullMode.DropOldest"/> this
    /// always returns <c>true</c>; the oldest event is evicted (and counted via the
    /// <c>itemDropped</c> callback) when the channel is at capacity.
    /// </summary>
    /// <param name="evt">The event to enqueue. Must not be <c>null</c>.</param>
    public void Enqueue(RequestMetricEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        _channel.Writer.TryWrite(evt);
    }

    /// <summary>
    /// Atomically reads and resets the dropped-event counter. Called by
    /// <see cref="RequestMetricsFlusher"/> once per <c>DropLogInterval</c> (~60 s) so
    /// drop loss is reported in a single WARN rather than per-drop (D-10, T-08-10).
    /// </summary>
    /// <returns>Number of events dropped since the last call.</returns>
    public long ConsumeDropCount() => Interlocked.Exchange(ref _droppedCount, 0L);
}
