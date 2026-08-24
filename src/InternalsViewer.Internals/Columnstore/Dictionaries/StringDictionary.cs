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

    public const int HandleArrayHeaderOffset = 0x38;

    public const int PageSizeArrayHeaderOffset = 0x44;

    [DataStructureItem(ItemType.DictionaryStringStore)]
    public StringDictionaryStore Store { get; set; } = new();

    [DataStructureItem(ItemType.DictionaryHandleArray)]
    public StringDictionaryArray HandleArray { get; set; } = new();

    [DataStructureItem(ItemType.DictionaryPageSizeArray)]
    public StringDictionaryArray PageSizeArray { get; set; } = new();

    public SubLobType SubLobType => Store.SubLobType;

    public int MaxStringSize => Store.MaxStringSize;

    public int StringCount => Store.StringCount;

    public int HandleSize => HandleArray.ElementSize;

    /// <summary>
    /// Handles the blob carries, which is the entry count as the blob itself records it
    /// </summary>
    public int HandleCount => HandleArray.ElementCount;

    public int PageCount => PageSizeArray.ElementCount;

    public StringHandle[] Handles { get; set; } = [];

    public int[] PageSizes { get; set; } = [];

    public StringPage[] Pages { get; set; } = [];

    public Encoding Encoding { get; set; } = Encoding.Latin1;

    /// <summary>
    /// Values read from a LOB page, by entry index, for the entries that hold a pointer rather than a string
    /// </summary>
    /// <remarks>
    /// The pointer cannot be followed while the blob is being parsed, the read needing the database, so the
    /// service fills these in once the dictionary is loaded.
    /// </remarks>
    public Dictionary<int, byte[]> LobValues { get; } = [];

    public string GetValue(long dataId) => GetValueAt(GetIndex(dataId));

    /// <summary>
    /// The pointer an entry holds, for an entry whose value was too big for the string store
    /// </summary>
    public bool TryGetLobPointer(int index, out StringLobPointer pointer)
    {
        pointer = default;

        if (index < 0 || index >= Handles.Length)
        {
            return false;
        }

        var handle = Handles[index];

        return handle.Page < Pages.Length
               && StringLobPointer.TryParse(Pages[handle.Page].GetValueBytes(handle.Offset), out pointer);
    }

    /// <summary>
    /// Raw entry bytes, which the column type rather than the dictionary decides how to read
    /// </summary>
    public byte[] GetValueBytes(long dataId) => GetValueBytesAt(GetIndex(dataId));

    public byte[] GetValueBytesAt(int index)
    {
        if (LobValues.TryGetValue(index, out var resolved))
        {
            return resolved;
        }

        var handle = Handles[index];

        return Pages[handle.Page].GetValueBytes(handle.Offset);
    }

    public string GetValueAt(int index)
    {
        if (LobValues.TryGetValue(index, out var resolved))
        {
            return Encoding.GetString(resolved);
        }

        var handle = Handles[index];

        return Pages[handle.Page].GetValue(handle.Offset, Encoding);
    }
}
