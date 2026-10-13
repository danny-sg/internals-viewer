using System.Buffers;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace.PublishedEvents;

internal sealed class PublishedEventLog
{
    private List<PublishedEvent> PublishedEvents { get; } = [];

    public void Add(IntPtr values, int count)
    {
        var size = count * Marshal.SizeOf<PublishedEvent>();

        var buffer = ArrayPool<byte>.Shared.Rent(size);

        try
        {
            Marshal.Copy(values, buffer, 0, size);

            PublishedEvents.AddRange(MemoryMarshal.Cast<byte, PublishedEvent>(buffer.AsSpan(0, size)));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public PublishedEvent[] Build() => [.. PublishedEvents];
}
