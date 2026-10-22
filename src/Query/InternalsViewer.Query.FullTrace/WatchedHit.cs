using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct WatchedHit(TracePosition Position, ulong Address, int DataOffset, uint Thread, ushort Size)
{
    public static WatchedHit From(in WatchHit hit, int dataOffset)
        => new(hit.Position, hit.Address, dataOffset, hit.Thread, (ushort)hit.Size);
}
