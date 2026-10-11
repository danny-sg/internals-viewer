using System.Runtime.InteropServices;

namespace InternalsViewer.Query.CallStack.TimeTravel.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct TimeTravelMemoryRead(ulong Function, int Register, uint AtReturn, uint Offset);
