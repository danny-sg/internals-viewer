using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct MemoryMapFunction(ulong Address, uint Operation, uint Reserved = 0);
