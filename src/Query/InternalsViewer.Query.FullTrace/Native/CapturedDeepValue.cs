using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CapturedDeepValue(uint Slot, uint Data, uint Length, uint Captured);
