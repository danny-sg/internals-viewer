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
    /// Start Data Id for the dictionary
    /// </summary>
    public int FirstId { get; set; }

    /// <summary>
    /// Last Data Id for the dictionary
    /// </summary>
    /// <remarks>
    /// Overflow dictionary takes over after the Last Id
    /// </remarks>
    public int LastId => FirstId + EntryCount - 1;

    protected int GetIndex(long dataId) => (int)(dataId - FirstId);
}
