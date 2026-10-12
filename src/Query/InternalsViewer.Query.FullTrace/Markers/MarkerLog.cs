using System.Buffers;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace.Markers;

internal sealed class MarkerLog
{
    private List<EventMarker> Markers { get; } = [];

    public void Add(IntPtr values, int count)
    {
        var size = count * Marshal.SizeOf<EventMarker>();

        var buffer = ArrayPool<byte>.Shared.Rent(size);

        try
        {
            Marshal.Copy(values, buffer, 0, size);

            Markers.AddRange(MemoryMarshal.Cast<byte, EventMarker>(buffer.AsSpan(0, size)));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public EventMarker[] Build() => [.. Markers];
}
