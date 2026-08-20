using InternalsViewer.Internals.Annotations;

namespace InternalsViewer.Internals.Columnstore.Dictionaries;

/// <summary>
/// Dictionary holding a flat array of numeric values
/// </summary>
public sealed class NumericDictionary : DictionaryBlob
{
    public const int HeaderSize = 56;

    [DataStructureItem(ItemType.DictionaryHashTable)]
    public NumericDictionaryHashTable HashTable { get; set; } = new();

    [DataStructureItem(ItemType.DictionaryValueArray)]
    public NumericDictionaryValueArray ValueArray { get; set; } = new();

    public int ElementSize => ValueArray.ElementSize;

    public int ValueCount => ValueArray.ValueCount;

    public long[] Values { get; set; } = [];

    public long GetValue(long dataId) => Values[GetIndex(dataId)];
}
