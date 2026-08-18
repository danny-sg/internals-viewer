using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Dictionaries;

/// <summary>
/// Parsed dictionary blob
/// </summary>
public abstract class DictionaryBlob : DataStructure
{
    public ReadOnlyMemory<byte> Data { get; set; }

    public int Version { get; set; }

    public ColumnstoreLobType LobType { get; set; }

    public int EntryCount { get; set; }

    /// <summary>
    /// Data id the first entry is addressed by, since ids do not start at zero
    /// </summary>
    public int FirstId { get; set; }

    public int GetIndex(long dataId) => (int)(dataId - FirstId);
}
