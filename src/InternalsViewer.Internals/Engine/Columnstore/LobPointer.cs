using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Engine.Columnstore;

/// <summary>
/// 16-byte LOB locator from syscscolsegments.data_ptr.
/// </summary>
public readonly record struct LobPointer(long BlobId, PageAddress PageAddress, short Slot)
{
    public bool IsEmpty => BlobId == 0 && Slot == 0;
}