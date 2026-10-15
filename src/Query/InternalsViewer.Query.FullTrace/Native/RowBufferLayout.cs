using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.RowBuffers;

namespace InternalsViewer.Query.FullTrace.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct RowBufferLayout(ulong VTable,
                                                uint BufferOffset,
                                                uint BindingsOffset,
                                                uint CountOffset = 0,
                                                RowBufferFormat Format = RowBufferFormat.Bindings);
