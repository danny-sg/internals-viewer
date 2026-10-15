using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct BufferColumn(uint Data,
                                             uint Length,
                                             uint Captured,
                                             int RecordOffset,
                                             uint BindingFlags,
                                             ushort FixedLength,
                                             byte Status,
                                             byte Flags);
