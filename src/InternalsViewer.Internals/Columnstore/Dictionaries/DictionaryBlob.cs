using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Dictionaries;

/// <summary>
/// Base Dictionary
/// </summary>
public abstract class DictionaryBlob : DataStructure
{
    public ReadOnlyMemory<byte> Data { get; set; }

    [DataStructureItem(ItemType.DictionaryVersion)]
    public int Version { get; set; }

    [DataStructureItem(ItemType.DictionaryLobType)]
    public ColumnstoreLobType LobType { get; set; }

    [DataStructureItem(ItemType.DictionaryReserved)]
    public int Reserved { get; set; }

    public int EntryCount { get; set; }

    /// <summary>
    /// Data id the first entry is addressed by, since ids do not start at zero
    /// </summary>
    public int FirstId { get; set; }

    protected int GetIndex(long dataId) => (int)(dataId - FirstId);
}
