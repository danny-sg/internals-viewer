using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Hooks;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct TraceHookFrame(ulong CallId, ulong StackPointer, ulong InstanceAddress, int CallNodeIndex);
