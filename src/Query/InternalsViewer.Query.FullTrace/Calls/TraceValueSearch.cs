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

            var found = new Dictionary<string, (int CallCount, int First, int Last)>();

            for (var callIndex = 0; callIndex < chunk.CallCount; callIndex++)
            {
                if (!IsIncluded(columns, callIndex, include))
                {
                    continue;
                }

                foreach (var location in GetLocations(columns, callIndex, value))
                {
                    found[location] = found.TryGetValue(location, out var counted)
                        ? (counted.CallCount + 1, counted.First, callIndex)
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

            for (var callIndex = 0; callIndex < chunk.CallCount; callIndex++)
            {
                if (IsIncluded(columns, callIndex, include) && GetLocations(columns, callIndex, value).Contains(use.Location))
                {
                    valueCalls.Add(GetValueCall(columns, callIndex, start));
                }
            }

            start += chunk.CallCount;
        }

        return valueCalls;
    }

    private bool IsIncluded(TraceCallColumnSet columns, int callIndex, Func<CallTreeNode?, bool>? include)
        => include is null || include(store.GetNode(columns.GetCallNodeIndex(callIndex)));

    private TraceValueCall GetValueCall(TraceCallColumnSet columns, int callIndex, int start)
        => new(start + callIndex, columns.GetFromPosition(callIndex), store.GetNode(columns.GetCallNodeIndex(callIndex)), columns.GetThread(callIndex));

    private static TraceValueUse MergeValueUse(TraceValueUse existing, TraceValueUse use) => existing with
    {
        CallCount = existing.CallCount + use.CallCount,
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

                start += chunk.CallCount;
            }
        }
    }

    private static IEnumerable<string> GetLocations(TraceCallColumnSet columns, int callIndex, ulong value)
    {
        for (var slot = 0; slot < ArgumentLayout.RegisterCount; slot++)
        {
            if (columns.GetRegister(callIndex, slot) == value)
            {
                yield return ArgumentLayout.Registers[slot];
            }
        }

        for (var slot = 0; slot < columns.StackSlotCount; slot++)
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
