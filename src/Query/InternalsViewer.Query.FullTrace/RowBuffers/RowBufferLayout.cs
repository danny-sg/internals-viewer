namespace InternalsViewer.Query.FullTrace.RowBuffers;

internal readonly record struct RowBufferLayout(ulong VTable,
                                                uint BufferOffset,
                                                uint BindingsOffset,
                                                uint CountOffset = 0,
                                                RowBufferFormat Format = RowBufferFormat.Bindings);
