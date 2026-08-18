namespace InternalsViewer.Internals.Columnstore.Metadata;

public readonly record struct RowGroupMetadataPointer(short ContainerId,
                                                      LobPointer Blob,
                                                      int Offset,
                                                      int Size)
{
    public bool IsEmpty => Blob.IsEmpty && Size == 0;
}