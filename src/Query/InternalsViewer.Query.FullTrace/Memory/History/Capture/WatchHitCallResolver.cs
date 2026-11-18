using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace.Memory.History.Capture;

internal sealed class WatchHitCallResolver(TraceTimeline? timeline)
{
    private Dictionary<int, ulong> Iterators { get; } = [];

    private Dictionary<int, (bool IsBufferPool, bool IsGrant)> PageSources { get; } = [];

    private TraceInnermostCallIndex Calls { get; } = new(timeline);

    public int GetWriter(uint thread, TracePosition position) => Calls.GetSpanId(thread, position);

    public int GetReplayNodeIndex(int spanId) => timeline?.GetSpanCall(spanId).ReplayNodeIndex ?? -1;

    public ulong GetInstructions(uint thread, TracePosition position)
        => timeline?.GetInnermostCall(thread, position) is { } span ? timeline.GetInstructions(span, position) : 0;

    public int GetReplayNodeIndex(uint thread, TracePosition position)
        => timeline?.GetInnermostCall(thread, position) is { } span ? span.ReplayNodeIndex : -1;

    public TracePosition? GetFrameStart(uint thread, TracePosition position, int replayNodeIndex)
    {
        foreach (var lane in timeline?.Threads ?? [])
        {
            if (lane.ThreadId != thread)
            {
                continue;
            }

            for (var row = lane.Rows.Count - 1; row >= 0; row--)
            {
                if (lane.Rows[row].GetIndex(position) is >= 0 and var index && lane.Rows[row].GetReplayNodeIndex(index) == replayNodeIndex)
                {
                    return lane.Rows[row].GetFromPosition(index);
                }
            }
        }

        return null;
    }

    public bool IsBufferPool(int replayNodeIndex, bool isClerkPage)
    {
        if (!PageSources.TryGetValue(replayNodeIndex, out var source))
        {
            CallTreeNode[] ancestors = [.. timeline?.GetNode(replayNodeIndex)?.GetAncestors() ?? []];

            source = (ancestors.Any(n => BufferPoolFunctionLookup.IsBufferPool(n.Frame)),
                      ancestors.Any(n => AllocationFunction.IsGrantFrame(n.Frame)));

            PageSources[replayNodeIndex] = source;
        }

        return !source.IsGrant && (isClerkPage || source.IsBufferPool);
    }

    public ulong GetIterator(int replayNodeIndex)
    {
        if (!Iterators.TryGetValue(replayNodeIndex, out var iterator))
        {
            iterator = timeline?.GetNode(replayNodeIndex)?.GetAncestors().FirstOrDefault(IsIteratorCall)?.Frame!.InstanceAddress ?? 0;

            Iterators[replayNodeIndex] = iterator;
        }

        return iterator;
    }

    private static bool IsIteratorCall(CallTreeNode node)
        => node.Frame is { InstanceAddress: not 0 } frame && IteratorSymbolLookup.IsIterator(frame);
}
