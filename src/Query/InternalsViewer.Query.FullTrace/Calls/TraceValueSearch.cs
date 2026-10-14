using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed class TraceValueSearch(TraceCallStore store)
{
    private static readonly string[] Registers = ["RCX", "RDX", "R8", "R9"];

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
                if (!IsIncluded(columns, chunk.Calls, call, include))
                {
                    continue;
                }

                foreach (var location in GetLocations(columns, chunk.Calls, call, value))
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
                                                GetValueCall(columns, chunk.Calls, first, start),
                                                GetValueCall(columns, chunk.Calls, last, start));

                    uses[(address, instance, location)] = uses.TryGetValue((address, instance, location), out var existing)
                        ? MergeValueUse(existing, use)
                        : use;
                }
            }
        });

        AddStackUses(uses, value, include, cancellationToken);

        return [.. uses.Values.OrderBy(u => u.First.Sequence)];
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
                if (IsIncluded(columns, chunk.Calls, call, include)
                    && GetLocations(columns, chunk.Calls, call, value).Contains(use.Location))
                {
                    valueCalls.Add(GetValueCall(columns, chunk.Calls, call, start));
                }
            }

            start += chunk.Calls;
        }

        return valueCalls;
    }

    private static string GetStackLocation(int position) => $"[RSP+0x{ArgumentLayout.StackOffset(position):X}]";

    private static int? GetStackPosition(string location)
    {
        for (var position = TraceArgumentCall.RegisterCount; position < ArgumentLayout.CapturedSlots; position++)
        {
            if (location == GetStackLocation(position))
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

                var location = GetStackLocation(locator.Register);

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

        return include is null || include(node) ? new TraceValueCall(call, argument.Sequence, node, argument.ThreadId) : null;
    }

    private bool IsIncluded(ulong[] columns, int calls, int call, Func<CallStackNode?, bool>? include)
        => include is null || include(store.GetNode((int)columns[TraceArgumentCall.NodeIndex * calls + call]));

    private TraceValueCall GetValueCall(ulong[] columns, int calls, int call, int start)
    {
        var node = (int)columns[TraceArgumentCall.NodeIndex * calls + call];

        var thread = (uint)columns[TraceArgumentCall.FlagsIndex * calls + call];

        return new TraceValueCall(start + call, columns[call], store.GetNode(node), thread);
    }

    private static TraceValueUse MergeValueUse(TraceValueUse existing, TraceValueUse use) => existing with
    {
        Calls = existing.Calls + use.Calls,
        First = use.First.Sequence < existing.First.Sequence ? use.First : existing.First,
        Last = use.Last.Sequence > existing.Last.Sequence ? use.Last : existing.Last
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

    private static IEnumerable<string> GetLocations(ulong[] columns, int calls, int call, ulong value)
    {
        var flags = columns[TraceArgumentCall.FlagsIndex * calls + call];

        for (var slot = 0; slot < TraceArgumentCall.RegisterCount; slot++)
        {
            if (columns[(TraceArgumentCall.IntegerOffset + slot) * calls + call] == value)
            {
                yield return Registers[slot];
            }
        }

        if ((flags & TraceArgumentCall.ReturnedFlag) != 0 && columns[TraceArgumentCall.ReturnValueIndex * calls + call] == value)
        {
            yield return "RAX";
        }
    }
}
