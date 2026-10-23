using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed class TraceValueSearch(TraceCallStore store)
{
    public Task<IReadOnlyList<TraceValueUse>> GetValueUsesAsync(ulong value,
                                                                CancellationToken cancellationToken,
                                                                Func<CallStackNode?, bool>? include = null)
        => Task.Run<IReadOnlyList<TraceValueUse>>(() => GetValueUses(value, include, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<TraceValueCall>> GetTraceValueCallsAsync(TraceValueUse use,
                                                                       ulong value,
                                                                       CancellationToken cancellationToken,
                                                                       Func<CallStackNode?, bool>? include = null)
        => Task.Run<IReadOnlyList<TraceValueCall>>(() => GetTraceValueCalls(use, value, include, cancellationToken), cancellationToken);

    private List<TraceValueUse> GetValueUses(ulong value, Func<CallStackNode?, bool>? include, CancellationToken cancellationToken)
    {
        var uses = new Dictionary<(ulong FunctionAddress, ulong InstanceAddress, string Location), TraceValueUse>();

        var sync = new Lock();

        var options = new ParallelOptions { CancellationToken = cancellationToken };

        Parallel.ForEach(GetScans(), options, () => Array.Empty<ulong>(), (scan, _, buffer) =>
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

        AddStackUses(uses, value, include, cancellationToken);

        return [.. uses.Values.OrderBy(u => u.First.Position)];
    }

    private List<TraceValueCall> GetTraceValueCalls(TraceValueUse use,
                                                    ulong value,
                                                    Func<CallStackNode?, bool>? include,
                                                    CancellationToken cancellationToken)
    {
        if (GetStackPosition(use.Location) is { } position)
        {
            return GetStackCalls(use, value, position, include, cancellationToken);
        }

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

    private static int? GetStackPosition(string location)
    {
        for (var position = ArgumentLayout.RegisterCount; position < ArgumentLayout.CapturedSlots; position++)
        {
            if (location == ArgumentLayout.GetStackLocation(position))
            {
                return position;
            }
        }

        return null;
    }

    private void AddStackUses(Dictionary<(ulong FunctionAddress, ulong InstanceAddress, string Location), TraceValueUse> uses,
                              ulong value,
                              Func<CallStackNode?, bool>? include,
                              CancellationToken cancellationToken)
    {
        foreach (var ((functionAddress, instanceAddress), table) in store.Locators)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TraceCallList? calls = null;

            foreach (var (callIndex, locator) in table.Entries())
            {
                if (locator.ValueType != LocatorValueType.Argument || locator.Value != value)
                {
                    continue;
                }

                calls ??= store.GetCallList(functionAddress, instanceAddress);

                if (GetStackValueCall(calls, callIndex, include) is not { } valueCall)
                {
                    continue;
                }

                var location = ArgumentLayout.GetStackLocation(locator.Register);

                var use = new TraceValueUse(functionAddress, instanceAddress, location, 1, valueCall, valueCall);

                uses[(functionAddress, instanceAddress, location)]
                    = uses.TryGetValue((functionAddress, instanceAddress, location), out var existing)
                        ? MergeValueUse(existing, use)
                        : use;
            }
        }
    }

    private List<TraceValueCall> GetStackCalls(TraceValueUse use,
                                               ulong value,
                                               int position,
                                               Func<CallStackNode?, bool>? include,
                                               CancellationToken cancellationToken)
    {
        var valueCalls = new List<TraceValueCall>();

        if (!store.Locators.TryGetValue((use.FunctionAddress, use.InstanceAddress), out var table)
            || store.GetCallList(use.FunctionAddress, use.InstanceAddress) is not { } calls)
        {
            return valueCalls;
        }

        foreach (var (callIndex, locator) in table.Entries())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (locator.ValueType == LocatorValueType.Argument
                && locator.Register == position
                && locator.Value == value
                && GetStackValueCall(calls, callIndex, include) is { } valueCall)
            {
                valueCalls.Add(valueCall);
            }
        }

        return valueCalls;
    }

    private TraceValueCall? GetStackValueCall(TraceCallList? calls, int callIndex, Func<CallStackNode?, bool>? include)
    {
        if (calls is null || callIndex >= calls.Count)
        {
            return null;
        }

        var argument = calls[callIndex];

        var node = store.GetNode(argument.CallNodeIndex);

        return include is null || include(node) ? new TraceValueCall(callIndex, argument.FromPosition, node, argument.ThreadId) : null;
    }

    private bool IsIncluded(TraceCallColumns columns, int callIndex, Func<CallStackNode?, bool>? include)
        => include is null || include(store.GetNode(columns.GetCallNodeIndex(callIndex)));

    private TraceValueCall GetValueCall(TraceCallColumns columns, int callIndex, int start)
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

    private static IEnumerable<string> GetLocations(TraceCallColumns columns, int callIndex, ulong value)
    {
        for (var slot = 0; slot < ArgumentLayout.RegisterCount; slot++)
        {
            if (columns.GetRegister(callIndex, slot) == value)
            {
                yield return ArgumentLayout.Registers[slot];
            }
        }

        if (columns.IsReturned(callIndex) && columns.GetReturnValue(callIndex) == value)
        {
            yield return ArgumentLayout.ReturnRegisterName;
        }
    }
}
