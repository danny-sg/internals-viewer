namespace InternalsViewer.Internals.Columnstore.Dictionaries;

/// <summary>
/// Dictionary holding a flat array of numeric values
/// </summary>
public sealed class NumericDictionary : DictionaryBlob
{
    public const int HeaderSize = 56;

    public int BucketSize { get; set; }

    public int BucketCount { get; set; }

    public int MaxLocalEntryCount { get; set; }

    public int HashEntrySize { get; set; }

    public int HashEntryCount { get; set; }

    public int CollisionCount { get; set; }

    public uint BucketIndexMask { get; set; }

    public int ElementSize { get; set; }

    public long[] Values { get; set; } = [];

    public long GetValue(long dataId) => Values[GetIndex(dataId)];
}
