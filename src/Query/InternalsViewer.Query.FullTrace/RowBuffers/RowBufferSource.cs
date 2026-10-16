using InternalsViewer.Query.FullTrace.RowBuffers.Enums;

namespace InternalsViewer.Query.FullTrace.RowBuffers;

public sealed record RowBufferSource(RowBufferSourceType Type,
                                     ulong Pointer = 0,
                                     ulong Iterator = 0,
                                     ulong Offset = 0,
                                     int Column = -1,
                                     int Row = -1);
