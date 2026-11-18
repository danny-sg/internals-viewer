using System.Runtime.InteropServices;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Memory;

namespace InternalsViewer.Query.FullTrace.Hooks;

internal static class TraceHookArgumentReader
{
    public const int StackSlotCount = ArgumentLayout.CapturedSlotCount - ArgumentLayout.RegisterCount;

    public static ulong GetArgument(ITraceReplay replay, in TraceHookCall call, int slot)
        => slot < ArgumentLayout.RegisterCount
            ? call.Arguments[slot]
            : replay.GetStackArguments(call)[slot - ArgumentLayout.RegisterCount];

    public static void ReadStackArguments(ITraceMemoryReader memory, in TraceHookCall call, Span<ulong> destination)
    {
        var read = memory.Read(GetStackArgumentAddress(memory, call), MemoryMarshal.AsBytes(destination)) / sizeof(ulong);

        destination[read..].Clear();
    }

    private static ulong GetStackArgumentAddress(ITraceMemoryReader memory, in TraceHookCall call)
    {
        var hasReturnAddress = memory.TryRead(call.StackPointer, out ulong top) && top == call.ReturnAddress;

        return call.StackPointer + (ulong)(hasReturnAddress ? ArgumentLayout.StackArgumentOffset : ArgumentLayout.HomeSpaceSize);
    }
}
