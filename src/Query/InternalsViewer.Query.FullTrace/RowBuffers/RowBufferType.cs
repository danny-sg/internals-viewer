namespace InternalsViewer.Query.FullTrace.RowBuffers;

public sealed record RowBufferType(int Ordinal, byte TypeId, int MaxLength, byte Precision, byte Scale, uint Collation);
