using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed class TraceValueSearch(TraceCallStore store)
{
    public Task<IReadOnlyList<TraceValueUse>> GetValueUsesAsync(ulong value,
                                                                CancellationToken cancellationToken,
                                                                Func<CallTreeNode?, bool>? include = null)
        => Task.Run<IReadOnlyList<TraceValueUse>>(() => GetValueUses(value, include, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<TraceValueCall>> GetTraceValueCallsAsync(TraceValueUse use,
                                                                       ulong value,
                                                                       CancellationToken cancellationToken,
                                                                       Func<CallTreeNode?, bool>? include = null)
        => Task.Run<IReadOnlyList<TraceValueCall>>(() => GetTraceValueCalls(use, value, include, cancellationToken), cancellationToken);

    private List<TraceValueUse> GetValueUses(ulong value, Func<CallTreeNode?, bool>? include, CancellationToken cancellationToken)
    {
        var uses = new Dictionary<(ulong FunctionAddress, ulong InstanceAddress, string Location), TraceValueUse>();

        var sync = new Lock();

        var options = new ParallelOptions { CancellationToken = cancellationToken };

        Parallel.ForEach(GetScans(), options, Array.Empty<ulong>, (scan, _, buffer) =>
        {
            var (functionAddress, instanceAddress, start, chunk) = scan;

            var columns = TraceCallStore.Decompress(chunk, ref buffer);

            var found = new Dictionary<string, (int Calls, int First, int Last)>();

            for (var callIndex = 0; callIndex < chunk.Calls; callIndex++)
            {
                if (!IsIncluded(columns, callIndex, include))
                {
                    continue;
                }

                foreach (var location in GetLocations(columns, callIndex, value))
                {
                    found[location] = found.TryGetValue(location, out var counted)
                        ? (counted.Calls + 1, counted.First, callIndex)
                        : (1, callIndex, callIndex);
                }
            }

            if (found.Count == 0)
            {
                return buffer;
            }

            lock (sync)
            {
                foreach (var (location, (calls, first, last)) in found)
                {
                    var use = new TraceValueUse(functionAddress,
                                                instanceAddress,
                                                location,
                                                calls,
                                                GetValueCall(columns, first, start),
                                                GetValueCall(columns, last, start));

                    uses[(functionAddress, instanceAddress, location)]
                        = uses.TryGetValue((functionAddress, instanceAddress, location), out var existing)
                            ? MergeValueUse(existing, use)
                            : use;
                }
            }

            return buffer;
        }, static _ => { });

        return [.. uses.Values.OrderBy(u => u.First.Position)];
    }

    private List<TraceValueCall> GetTraceValueCalls(TraceValueUse use,
                                                    ulong value,
                                                    Func<CallTreeNode?, bool>? include,
                                                    CancellationToken cancellationToken)
    {
        var valueCalls = new List<TraceValueCall>();

        if (!store.Functions.TryGetValue((use.FunctionAddress, use.InstanceAddress), out var chunks))
        {
            return valueCalls;
        }

        var start = 0;

        ulong[] buffer = [];

        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var columns = TraceCallStore.Decompress(chunk, ref buffer);

            for (var callIndex = 0; callIndex < chunk.Calls; callIndex++)
            {
                if (IsIncluded(columns, callIndex, include) && GetLocations(columns, callIndex, value).Contains(use.Location))
                {
                    valueCalls.Add(GetValueCall(columns, callIndex, start));
                }
            }

            start += chunk.Calls;
        }

        return valueCalls;
    }

    private bool IsIncluded(TraceCallChunk columns, int callIndex, Func<CallTreeNode?, bool>? include)
        => include is null || include(store.GetNode(columns.GetCallNodeIndex(callIndex)));

    private TraceValueCall GetValueCall(TraceCallChunk columns, int callIndex, int start)
        => new(start + callIndex, columns.GetFromPosition(callIndex), store.GetNode(columns.GetCallNodeIndex(callIndex)), columns.GetThread(callIndex));

    private static TraceValueUse MergeValueUse(TraceValueUse existing, TraceValueUse use) => existing with
    {
        Calls = existing.Calls + use.Calls,
        First = use.First.Position < existing.First.Position ? use.First : existing.First,
        Last = use.Last.Position > existing.Last.Position ? use.Last : existing.Last
    };

    private IEnumerable<(ulong FunctionAddress, ulong InstanceAddress, int Start, TraceCallStore.Chunk Chunk)> GetScans()
    {
        foreach (var ((functionAddress, instanceAddress), chunks) in store.Functions)
        {
            var start = 0;

            foreach (var chunk in chunks)
            {
                yield return (functionAddress, instanceAddress, start, chunk);

                start += chunk.Calls;
            }
        }
    }

    private static IEnumerable<string> GetLocations(TraceCallChunk columns, int callIndex, ulong value)
    {
        for (var slot = 0; slot < ArgumentLayout.RegisterCount; slot++)
        {
            if (columns.GetRegister(callIndex, slot) == value)
            {
                yield return ArgumentLayout.Registers[slot];
            }
        }

        for (var slot = 0; slot < columns.StackSlots; slot++)
        {
            if (columns.GetStackValue(callIndex, ArgumentLayout.RegisterCount + slot) == value)
            {
                yield return ArgumentLayout.GetStackLocation(ArgumentLayout.RegisterCount + slot);
            }
        }

        if (columns.IsReturned(callIndex) && columns.GetReturnValue(callIndex) == value)
        {
            yield return ArgumentLayout.ReturnRegisterName;
        }
    }
}
