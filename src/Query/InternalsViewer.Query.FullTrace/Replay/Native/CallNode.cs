using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Replay.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CallNode(int ParentIndex, ulong FunctionAddress, ulong InstanceAddress, ulong CallCount);
