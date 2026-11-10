using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;

namespace InternalsViewer.Query.FullTrace.Calls;

internal sealed class StackArgumentHook(AddressTable<int> functions, TraceCallStore.Builder store) : ITraceHook
{
    private const int StackSlots = ArgumentLayout.CapturedSlotCount - ArgumentLayout.RegisterCount;

    public IEnumerable<TraceHookFunction> Functions => functions.Keys.Select(f => new TraceHookFunction(f, TraceHookEvents.Entry));

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (!functions.TryGetValue(call.FunctionAddress, out var count))
        {
            return;
        }

        Span<ulong> values = stackalloc ulong[StackSlots];

        var stack = values[..Math.Min(count, StackSlots)];

        var read = replay.Read(TraceHookArgumentReader.GetStackArguments(replay, call), MemoryMarshal.AsBytes(stack)) / sizeof(ulong);

        stack[read..].Clear();

        store.AddStackArguments(call.FunctionAddress, call.InstanceAddress, call.CallIndex, stack);
    }
}
