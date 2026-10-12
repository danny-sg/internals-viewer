using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct PointerValue(ulong Function,
                                             ulong Instance,
                                             ulong Value,
                                             uint Call,
                                             int Register,
                                             uint Offset = 0,
                                             uint AtReturn = 0);
