using InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;

namespace InternalsViewer.Internals.Services.Loaders.Records.Fields;

/// <summary>
/// Loads a LOB field, dispatching to the correct loader based on the Blob field type in the first byte
/// </summary>
public static class LobFieldLoader
{
    public static BlobField? Load(byte[] data, int offset, bool isMarkEnabled)
    {
        return (BlobFieldType)data[0] switch
        {
            BlobFieldType.LobPointer => LobPointerFieldLoader.Load(data, offset, isMarkEnabled),
            BlobFieldType.LobRoot => LobRootFieldLoader.Load(data, offset, isMarkEnabled),
            BlobFieldType.RowOverflow => LobOverflowFieldLoader.Load(data, offset, isMarkEnabled),
            _ => null
        };
    }
}