namespace InternalsViewer.Query.FullTrace.RowBuffers;

internal readonly record struct RawColumn(uint Data,
                                          uint Length,
                                          uint Captured,
                                          int RecordOffset,
                                          uint BindingFlags,
                                          ushort FixedLength,
                                          byte Status,
                                          byte Flags);
