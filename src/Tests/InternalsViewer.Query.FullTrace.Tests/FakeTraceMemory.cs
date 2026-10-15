using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Hooks;

namespace InternalsViewer.Query.FullTrace.Tests;

internal sealed class FakeTraceMemory : ITraceMemory
{
    private Dictionary<ulong, byte> Bytes { get; } = [];

    public FakeTraceMemory Write(ulong address, ReadOnlySpan<byte> data)
    {
        for (var index = 0; index < data.Length; index++)
        {
            Bytes[address + (ulong)index] = data[index];
        }

        return this;
    }

    public FakeTraceMemory Write<T>(ulong address, T value) where T : unmanaged
        => Write(address, MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in value)));

    public int Read(ulong address, Span<byte> destination)
    {
        for (var index = 0; index < destination.Length; index++)
        {
            if (!Bytes.TryGetValue(address + (ulong)index, out var value))
            {
                return index;
            }

            destination[index] = value;
        }

        return destination.Length;
    }
}
