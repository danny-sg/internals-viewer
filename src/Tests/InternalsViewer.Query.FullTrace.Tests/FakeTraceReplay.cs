using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.Tests;

internal sealed class FakeTraceReplay : ITraceReplay
{
    public List<(uint Thread, ulong Frame)> WatchedFrames { get; } = [];

    public List<(uint Thread, ulong StackPointer)> WatchedStacks { get; } = [];

    public TraceHookFrame[] Frames { get; set; } = [];

    private Dictionary<ulong, byte> Bytes { get; } = [];

    public FakeTraceReplay Write(ulong address, ReadOnlySpan<byte> data)
    {
        for (var index = 0; index < data.Length; index++)
        {
            Bytes[address + (ulong)index] = data[index];
        }

        return this;
    }

    public FakeTraceReplay Write<T>(ulong address, T value) where T : unmanaged
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

    public bool WatchFrame(uint thread, ulong frame)
    {
        WatchedFrames.Add((thread, frame));

        return true;
    }

    public void WatchStack(uint thread, ulong stackPointer) => WatchedStacks.Add((thread, stackPointer));

    public ReadOnlySpan<TraceHookFrame> GetFrames(uint thread) => Frames;
}
