using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.RowBuffers.Parsers;
using InternalsViewer.Query.FullTrace.RowSources;

namespace InternalsViewer.Query.FullTrace.RowBuffers.Accessors;

internal sealed class AccessorHook(AccessorFunctions functions, RowSourceHook rowSources) : ITraceHook
{
    private const int CountArgument = 2;

    private const int BindingsArgument = 3;

    private const int HandleArgument = 5;

    public IEnumerable<TraceHookFunction> Functions
        => [.. functions.Functions.Select(f => new TraceHookFunction(f, TraceHookEvents.Entry | TraceHookEvents.Return))];

    private Dictionary<(ulong Iterator, ulong ValueOffset), RowBufferType> Types { get; } = [];

    private Dictionary<ulong, PendingAccessor> Creating { get; } = [];

    private Dictionary<ulong, int[]> Ordinals { get; } = [];

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        var bindings = AccessorBindingParser.Parse(replay, call.Arguments[BindingsArgument], call.Arguments[CountArgument]);

        if (bindings.Length == 0)
        {
            return;
        }

        var iterator = rowSources.GetCurrentIterator(call.Thread);

        foreach (var binding in bindings)
        {
            if (iterator != 0 && binding.Type is { } type)
            {
                Types.TryAdd((iterator, binding.ValueOffset), type);
            }
        }

        if (replay.TryReadArgument(call, HandleArgument, out var handle) && handle >= ColumnValueParser.SmallestAddress)
        {
            Creating[call.Id] = new PendingAccessor(handle, [.. bindings.Select(b => b.Ordinal)]);
        }
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (Creating.Remove(call.Id, out var pending)
            && (int)call.ReturnValue >= 0
            && replay.TryRead(pending.Handle, out ulong accessor)
            && accessor != 0)
        {
            Ordinals[accessor] = pending.Ordinals;
        }
    }

    public void OnAbandoned(in TraceHookCall call) => Creating.Remove(call.Id);

    public RowBufferType? GetColumnType(ulong iterator, ulong valueOffset) => Types.GetValueOrDefault((iterator, valueOffset));

    public int GetOrdinal(ulong accessor) => Ordinals.TryGetValue(accessor, out var ordinals) && ordinals.Length == 1 ? ordinals[0] : -1;

    private readonly record struct PendingAccessor(ulong Handle, int[] Ordinals);
}
