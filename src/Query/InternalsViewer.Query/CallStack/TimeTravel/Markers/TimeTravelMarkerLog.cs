using System.Buffers;
using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack.TimeTravel.Native;

namespace InternalsViewer.Query.CallStack.TimeTravel.Markers;

internal sealed class TimeTravelMarkerLog
{
    private List<TimeTravelMarker> Markers { get; } = [];

    public void Add(IntPtr values, int count)
    {
        var size = count * Marshal.SizeOf<TimeTravelMarker>();

        var buffer = ArrayPool<byte>.Shared.Rent(size);

        try
        {
            Marshal.Copy(values, buffer, 0, size);

            Markers.AddRange(MemoryMarshal.Cast<byte, TimeTravelMarker>(buffer.AsSpan(0, size)));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public TimeTravelMarker[] Build() => [.. Markers];
}
