using System.Runtime.InteropServices;
using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.FullTrace.Timeline.Ticks;

public sealed class TraceTickSeries
{
    private TraceTickSeries(Dictionary<ExecutionOperatorEvent, Dictionary<uint, TraceTickIndex>> indexes)
    {
        Indexes = indexes;

        Count = indexes.Values.Sum(t => t.Values.Sum(i => i.Count));

        Bytes = indexes.Values.Sum(t => t.Values.Sum(i => i.Bytes));
    }

    public static TraceTickSeries Empty { get; }
        = new(new Dictionary<ExecutionOperatorEvent, Dictionary<uint, TraceTickIndex>>(ReferenceEqualityComparer.Instance));

    public int Count { get; }

    public long Bytes { get; }

    private Dictionary<ExecutionOperatorEvent, Dictionary<uint, TraceTickIndex>> Indexes { get; }

    public TraceTickIndex? GetTicks(ExecutionOperatorEvent operatorEvent, uint thread)
        => Indexes.TryGetValue(operatorEvent, out var threads) ? threads.GetValueOrDefault(thread) : null;

    internal IEnumerable<TraceTickIndex> AllIndexes => Indexes.Values.SelectMany(t => t.Values);

    public IEnumerable<ExecutionOperatorEvent> GetRunning(TracePosition position)
        => Indexes.Where(o => o.Value.Values.Any(i => i.IsRunning(position))).Select(o => o.Key);

    public IEnumerable<ExecutionOperatorEvent> GetEnding(TracePosition after, TracePosition upTo, Func<byte, bool> include)
        => Indexes.Where(o => o.Value.Values.Any(i => i.HasEndBetween(after, upTo, include))).Select(o => o.Key);

    public IEnumerable<TraceTick> GetRunningTicks(TracePosition position) => AllIndexes.SelectMany(i => i.GetRunningTicks(position));

    public IEnumerable<TraceTick> GetEndedTicks(TracePosition after, TracePosition upTo)
        => AllIndexes.SelectMany(i => i.GetEndedTicks(after, upTo));

    public IEnumerable<TraceTick> GetTicks(Func<byte, bool> include)
    {
        foreach (var index in AllIndexes)
        {
            for (var position = 0; position < index.Count; position++)
            {
                if (include(index.GetValue(position)))
                {
                    yield return index.Get(position);
                }
            }
        }
    }

    public IEnumerable<TraceTick> GetLatestTicks(TracePosition position)
    {
        foreach (var threads in Indexes.Values)
        {
            TraceTick? latest = null;

            foreach (var index in threads.Values)
            {
                if (index.GetLatest(position) is { } tick
                    && (latest is not { } current || tick.Span.From.Position > current.Span.From.Position))
                {
                    latest = tick;
                }
            }

            if (latest is { } found)
            {
                yield return found;
            }
        }
    }

    public IEnumerable<(ExecutionOperatorEvent Operator, int Count)> CountEnded(IReadOnlyCollection<byte> values, TracePosition upTo)
    {
        foreach (var (operatorEvent, threads) in Indexes)
        {
            var count = threads.Values.Sum(i => values.Sum(v => i.CountEnded(v, upTo)));

            if (count > 0)
            {
                yield return (operatorEvent, count);
            }
        }
    }

    internal static TraceTickSeries Create(TraceTimeline timeline, IEnumerable<TraceTickSource> sources)
    {
        var grouped = new Dictionary<ExecutionOperatorEvent, Dictionary<uint, List<TraceTickSource>>>(ReferenceEqualityComparer.Instance);

        foreach (var source in sources)
        {
            if (source.Operator is not { } operatorEvent)
            {
                continue;
            }

            ref var threads = ref CollectionsMarshal.GetValueRefOrAddDefault(grouped, operatorEvent, out _);

            threads ??= [];

            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(threads, source.Thread.ThreadId, out _);

            (list ??= []).Add(source);
        }

        var indexes = new Dictionary<ExecutionOperatorEvent, Dictionary<uint, TraceTickIndex>>(ReferenceEqualityComparer.Instance);

        foreach (var (operatorEvent, threads) in grouped)
        {
            indexes[operatorEvent] = threads.ToDictionary(t => t.Key,
                                                          t => new TraceTickIndex(timeline, t.Value[0].Thread, operatorEvent, t.Value));
        }

        return new TraceTickSeries(indexes);
    }
}
