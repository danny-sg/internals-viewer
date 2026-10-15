using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Hooks;

internal static class TraceMemoryExtensions
{
    public static bool TryRead<T>(this ITraceMemory memory, ulong address, out T value) where T : unmanaged
    {
        value = default;

        return memory.Read(address, MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1))) == Unsafe.SizeOf<T>();
    }
}
