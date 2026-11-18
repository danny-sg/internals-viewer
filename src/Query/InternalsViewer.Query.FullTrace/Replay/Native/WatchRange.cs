using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Replay.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct WatchRange(ulong Address, ulong Size);
