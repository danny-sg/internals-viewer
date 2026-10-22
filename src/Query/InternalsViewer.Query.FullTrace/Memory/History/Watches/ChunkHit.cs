using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace.Memory.History.Watches;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct ChunkHit(TracePosition Position, ulong Address, int DataOffset, uint Thread, ushort Size)
{
    public static ChunkHit From(in WatchHit hit, int dataOffset)
        => new(hit.Position, hit.Address, dataOffset, hit.Thread, (ushort)hit.Size);
}
