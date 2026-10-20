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
        var uses = new Dictionary<(ulong Address, ulong Instance, string Location), TraceValueUse>();

        var sync = new Lock();

        var options = new ParallelOptions { CancellationToken = cancellationToken };

        Parallel.ForEach(GetScans(), options, scan =>
        {
            var (address, instance, start, chunk) = scan;

            var columns = TraceCallStore.Decompress(chunk);

            var found = new Dictionary<string, (int Calls, int First, int Last)>();

            for (var call = 0; call < chunk.Calls; call++)
            {
                if (!IsIncluded(columns, call, include))
                {
                    continue;
                }

                foreach (var location in GetLocations(columns, call, value))
                {
                    found[location] = found.TryGetValue(location, out var counted)
                        ? (counted.Calls + 1, counted.First, call)
                        : (1, call, call);
                }
            }

            if (found.Count == 0)
            {
                return;
            }

            lock (sync)
            {
                foreach (var (location, (calls, first, last)) in found)
                {
                    var use = new TraceValueUse(address,
                                                instance,
                                                location,
                                                calls,
                                                GetValueCall(columns, first, start),
                                                GetValueCall(columns, last, start));

                    uses[(address, instance, location)] = uses.TryGetValue((address, instance, location), out var existing)
                        ? MergeValueUse(existing, use)
                        : use;
                }
            }
        });

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

        if (!store.Functions.TryGetValue((use.Address, use.Instance), out var chunks))
        {
            return valueCalls;
        }

        var start = 0;

        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var columns = TraceCallStore.Decompress(chunk);

            for (var call = 0; call < chunk.Calls; call++)
            {
                if (IsIncluded(columns, call, include) && GetLocations(columns, call, value).Contains(use.Location))
                {
                    valueCalls.Add(GetValueCall(columns, call, start));
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

    private void AddStackUses(Dictionary<(ulong Address, ulong Instance, string Location), TraceValueUse> uses,
                              ulong value,
                              Func<CallStackNode?, bool>? include,
                              CancellationToken cancellationToken)
    {
        foreach (var ((address, instance), table) in store.Locators)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TraceCallList? calls = null;

            foreach (var (call, locator) in table.Entries())
            {
                if (locator.ValueType != LocatorValueType.Argument || locator.Value != value)
                {
                    continue;
                }

                calls ??= store.GetCallList(address, instance);

                if (GetStackValueCall(calls, call, include) is not { } valueCall)
                {
                    continue;
                }

                var location = ArgumentLayout.GetStackLocation(locator.Register);

                var use = new TraceValueUse(address, instance, location, 1, valueCall, valueCall);

                uses[(address, instance, location)] = uses.TryGetValue((address, instance, location), out var existing)
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

        if (!store.Locators.TryGetValue((use.Address, use.Instance), out var table)
            || store.GetCallList(use.Address, use.Instance) is not { } calls)
        {
            return valueCalls;
        }

        foreach (var (call, locator) in table.Entries())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (locator.ValueType == LocatorValueType.Argument
                && locator.Register == position
                && locator.Value == value
                && GetStackValueCall(calls, call, include) is { } valueCall)
            {
                valueCalls.Add(valueCall);
            }
        }

        return valueCalls;
    }

    private TraceValueCall? GetStackValueCall(TraceCallList? calls, int call, Func<CallStackNode?, bool>? include)
    {
        if (calls is null || call >= calls.Count)
        {
            return null;
        }

        var argument = calls[call];

        var node = store.GetNode(argument.Node);

        return include is null || include(node) ? new TraceValueCall(call, argument.FromPosition, node, argument.ThreadId) : null;
    }

    private bool IsIncluded(TraceCallColumns columns, int call, Func<CallStackNode?, bool>? include)
        => include is null || include(store.GetNode(columns.GetNodeIndex(call)));

    private TraceValueCall GetValueCall(TraceCallColumns columns, int call, int start)
        => new(start + call, columns.GetFromPosition(call), store.GetNode(columns.GetNodeIndex(call)), columns.GetThread(call));

    private static TraceValueUse MergeValueUse(TraceValueUse existing, TraceValueUse use) => existing with
    {
        Calls = existing.Calls + use.Calls,
        First = use.First.Position < existing.First.Position ? use.First : existing.First,
        Last = use.Last.Position > existing.Last.Position ? use.Last : existing.Last
    };

    private IEnumerable<(ulong Address, ulong Instance, int Start, TraceCallStore.Chunk Chunk)> GetScans()
    {
        foreach (var ((address, instance), chunks) in store.Functions)
        {
            var start = 0;

            foreach (var chunk in chunks)
            {
                yield return (address, instance, start, chunk);

                start += chunk.Calls;
            }
        }
    }

    private static IEnumerable<string> GetLocations(TraceCallColumns columns, int call, ulong value)
    {
        for (var slot = 0; slot < ArgumentLayout.RegisterCount; slot++)
        {
            if (columns.GetRegister(call, slot) == value)
            {
                yield return ArgumentLayout.Registers[slot];
            }
        }

        if (columns.IsReturned(call) && columns.GetReturnValue(call) == value)
        {
            yield return ArgumentLayout.ReturnRegisterName;
        }
    }
}
