using System.Runtime.InteropServices;

namespace InternalsViewer.Query.CallStack.TimeTravel;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct TimeTravelCallActivity(int Node, int Slice, ulong Calls);
