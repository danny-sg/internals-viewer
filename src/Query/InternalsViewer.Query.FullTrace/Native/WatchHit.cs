using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct WatchHit(ulong Sequence,
                                         ulong Steps,
                                         ulong Address,
                                         ulong ProgramCounter,
                                         uint Thread,
                                         uint Size,
                                         uint Offset,
                                         uint Type)
{
    public TracePosition Position => new(Sequence, Steps);
}
