using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.Layouts;

namespace InternalsViewer.Query.FullTrace.Locators;

internal sealed class ImageFallbackMemory(ITraceReplay replay, ImageText images) : ITraceMemoryReader
{
    public ITraceReplay Replay => replay;

    public EngineLayout Layout => replay.Layout;

    public int Read(ulong address, Span<byte> destination)
    {
        var count = replay.Read(address, destination);

        return count < destination.Length
            ? count + images.Read(address + (ulong)count, destination[count..], isReadOnly: true)
            : count;
    }
}
