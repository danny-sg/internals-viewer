using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Hooks;

internal static class TraceReplayExtensions
{
    public static bool TryRead<T>(this ITraceReplay replay, ulong address, out T value) where T : unmanaged
    {
        value = default;

        return replay.Read(address, MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1))) == Unsafe.SizeOf<T>();
    }

    public static ulong GetStackArguments(this ITraceReplay replay, in TraceHookCall call)
    {
        var hasReturnAddress = replay.TryRead(call.StackPointer, out ulong top) && top == call.ReturnAddress;

        return call.StackPointer + (ulong)(hasReturnAddress ? ArgumentLayout.StackArgumentOffset : ArgumentLayout.HomeSpaceSize);
    }
}
