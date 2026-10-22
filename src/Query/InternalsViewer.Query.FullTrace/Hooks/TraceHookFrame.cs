using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Hooks;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct TraceHookFrame(ulong Id, ulong StackPointer, ulong InstanceAddress, int CallNodeIndex, uint Reserved = 0);
