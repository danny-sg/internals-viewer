using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CallNode(int Parent, uint Reserved, ulong Address, ulong Instance, ulong Calls);
