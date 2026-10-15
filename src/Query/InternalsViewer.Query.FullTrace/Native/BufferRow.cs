using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.RowBuffers;

namespace InternalsViewer.Query.FullTrace.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct BufferRow(ulong Function,
                                          ulong Instance,
                                          ulong Iterator,
                                          ulong Buffer,
                                          ulong Id,
                                          uint Call,
                                          uint FirstColumn,
                                          uint ColumnCount,
                                          RowBufferFormat Format = RowBufferFormat.Bindings);
