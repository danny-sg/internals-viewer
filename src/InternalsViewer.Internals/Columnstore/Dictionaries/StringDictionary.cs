using System.Text;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Dictionaries;

/// <summary>
/// Dictionary holding string values in one or more pages addressed by a handle array
/// </summary>
public sealed class StringDictionary : DictionaryBlob
{
    public const int HandleArrayOffset = 0x50;

    /// <summary>
    /// Opens the string store header, which follows the blob header rather than starting a structure of its own
    /// </summary>
    [DataStructureItem(ItemType.DictionarySubLobType)]
    public SubLobType SubLobType { get; set; }

    [DataStructureItem(ItemType.DictionaryMaxStringSize)]
    public int MaxStringSize { get; set; }

    /// <summary>
    /// Strings the store holds, which runs one short of the entry count the metadata carries
    /// </summary>
    [DataStructureItem(ItemType.DictionaryStringCount)]
    public int StringCount { get; set; }

    [DataStructureItem(ItemType.DictionaryHandleSize)]
    public int HandleSize { get; set; }

    /// <summary>
    /// Handles the blob carries, which is the entry count as the blob itself records it
    /// </summary>
    [DataStructureItem(ItemType.DictionaryHandleCount)]
    public int HandleCount { get; set; }

    [DataStructureItem(ItemType.DictionaryPageCount)]
    public int PageCount { get; set; }

    /// <summary>
    /// The run between the string store header and the handle size, which has held nothing but zeros so far
    /// </summary>
    [DataStructureItem(ItemType.DictionaryReserved)]
    public byte[] Reserved18 { get; set; } = [];

    [DataStructureItem(ItemType.DictionaryUnknown)]
    public int Unknown44 { get; set; }

    [DataStructureItem(ItemType.DictionaryUnknown)]
    public int Unknown48 { get; set; }

    public StringHandle[] Handles { get; set; } = [];

    public int[] PageSizes { get; set; } = [];

    public StringPage[] Pages { get; set; } = [];

    public Encoding Encoding { get; set; } = Encoding.Latin1;

    public string GetValue(long dataId) => GetValueAt(GetIndex(dataId));

    /// <summary>
    /// Raw entry bytes, which the column type rather than the dictionary decides how to read
    /// </summary>
    public byte[] GetValueBytes(long dataId)
    {
        var handle = Handles[GetIndex(dataId)];

        return Pages[handle.Page].GetValueBytes(handle.Offset);
    }

    public string GetValueAt(int index)
    {
        var handle = Handles[index];

        return Pages[handle.Page].GetValue(handle.Offset, Encoding);
    }
}
