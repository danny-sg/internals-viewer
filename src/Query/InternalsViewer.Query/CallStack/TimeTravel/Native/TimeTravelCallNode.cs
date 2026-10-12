using System.Runtime.InteropServices;

namespace InternalsViewer.Query.CallStack.TimeTravel.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct TimeTravelCallNode(int Parent, uint Reserved, ulong Address, ulong Instance, ulong Calls);
