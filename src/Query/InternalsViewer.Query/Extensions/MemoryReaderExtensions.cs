using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.Extensions;

public static class MemoryReaderExtensions
{
    public static bool TryRead<T>(this IMemoryReader memory, ulong address, out T value) where T : unmanaged
    {
        value = default;

        return memory.Read(address, MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1))) == Unsafe.SizeOf<T>();
    }
}
