namespace InternalsViewer.Query.FullTrace.RowBuffers;

internal readonly record struct RawRow(ulong Function,
                                       ulong Instance,
                                       ulong Iterator,
                                       ulong Buffer,
                                       ulong Id,
                                       uint Call,
                                       uint FirstColumn,
                                       uint ColumnCount,
                                       RowBufferFormat Format = RowBufferFormat.Bindings);
