using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Hooks;

internal static class TraceReplayExtensions
{
    private const int RegisterArguments = 4;

    public static bool TryRead<T>(this ITraceReplay replay, ulong address, out T value) where T : unmanaged
    {
        value = default;

        return replay.Read(address, MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1))) == Unsafe.SizeOf<T>();
    }

    public static ulong GetStackArguments(this ITraceReplay replay, in TraceHookCall call)
    {
        var homeSpace = replay.TryRead(call.StackPointer, out ulong top) && top == call.ReturnAddress
                        ? call.StackPointer + sizeof(ulong)
                        : call.StackPointer;

        return homeSpace + RegisterArguments * sizeof(ulong);
    }
}
