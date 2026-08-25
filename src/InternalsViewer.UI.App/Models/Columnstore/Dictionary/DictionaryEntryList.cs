using System;
using System.Collections;
using System.Collections.Generic;
using InternalsViewer.Internals.Columnstore.Dictionaries;

namespace InternalsViewer.UI.App.Models.Columnstore.Dictionary;

/// <summary>
/// One dictionary entry, decoded only when something asks for it
/// </summary>
public sealed class DictionaryEntryDetail(DictionaryBlob blob, int index, bool showDerivation)
    : IEquatable<DictionaryEntryDetail>
{
    public int Index { get; } = index;

    public bool ShowDerivation { get; } = showDerivation;

    public long DataId => blob.FirstId + Index;

    public string PageDescription => blob is StringDictionary strings ? $"{strings.Handles[Index].Page}" : string.Empty;

    /// <summary>
    /// Where the value sits in the blob, a numeric dictionary holding its values in a flat array after the header
    /// </summary>
    public int ValueOffset => blob is NumericDictionary numbers
        ? NumericDictionary.HeaderSize + (Index * numbers.ElementSize)
        : -1;

    public int ValueSize => blob is NumericDictionary numbers ? numbers.ElementSize : 0;

    /// <summary>
    /// Where in its page the entry starts, counted in bits on a Huffman coded page and bytes on a plain one
    /// </summary>
    public string OffsetDescription => blob is StringDictionary strings
        ? $"{strings.Handles[Index].Offset}"
        : $"0x{ValueOffset:X}";

    /// <summary>
    /// Bytes the entry takes, a numeric one being a fixed element and a string one whatever it decodes to
    /// </summary>
    public int Length => blob is NumericDictionary numbers ? numbers.ElementSize : StoredBytes?.Length ?? 0;

    public string Value => field ??= Decode();

    /// <summary>
    /// Working from the data id to the entry it addresses, the ids not starting at zero
    /// </summary>
    public ValueDerivation Derivation => field ??= new ValueDerivation
    {
        Steps =
        [
            new DerivationStep { Name = "Data Id", Value = $"{DataId}" },
            new DerivationStep { Operator = "-", Name = "First Id", Value = $"{blob.FirstId}" }
        ],
        Result = $"{Index}"
    };

    private byte[]? StoredBytes
        => field ??= blob is StringDictionary strings ? strings.GetValueBytesAt(Index) : null;

    public bool Equals(DictionaryEntryDetail? other) => other is not null && other.Index == Index;

    public override bool Equals(object? obj) => Equals(obj as DictionaryEntryDetail);

    public override int GetHashCode() => Index;

    private string Decode()
    {
        try
        {
            return blob switch
            {
                StringDictionary strings => strings.GetValueAt(Index),
                NumericDictionary numbers => $"{numbers.Values[Index]}",
                _ => string.Empty
            };
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
    }
}

/// <summary>
/// Every entry of a dictionary as an indexed list, decoded one entry at a time as the grid asks for them
/// </summary>
/// <remarks>
/// A global dictionary runs to hundreds of thousands of entries, and a Huffman coded one costs a bit level decode
/// per entry, so nothing is decoded until it is on screen. Sorting and filtering have to stay off, both of them
/// reading every entry to do their work.
/// </remarks>
/// <remarks>
/// An index map narrows the list to a subset of the dictionary, such as the entries living on one page, without
/// any of them being built to do it.
/// </remarks>
public sealed class DictionaryEntryList(DictionaryBlob blob, bool showDerivation, int[]? indexes = null)
    : IList<DictionaryEntryDetail>, IList
{
    public int Count { get; } = indexes?.Length ?? blob.EntryCount;

    public bool IsReadOnly => true;

    public bool IsFixedSize => true;

    public bool IsSynchronized => false;

    public object SyncRoot { get; } = new();

    public DictionaryEntryDetail this[int index]
    {
        get => (uint)index < (uint)Count
            ? new DictionaryEntryDetail(blob, indexes?[index] ?? index, showDerivation)
            : throw new ArgumentOutOfRangeException(nameof(index));
        set => throw new NotSupportedException();
    }

    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException();
    }

    public int IndexOf(DictionaryEntryDetail item)
        => indexes is null
            ? item.Index >= 0 && item.Index < Count ? item.Index : -1
            : Array.IndexOf(indexes, item.Index);

    /// <summary>
    /// The entry standing at a dictionary index, which a narrowed list holds at a row of its own
    /// </summary>
    public DictionaryEntryDetail? Find(int entryIndex)
    {
        var position = indexes is null ? entryIndex : Array.IndexOf(indexes, entryIndex);

        return position >= 0 && position < Count ? this[position] : null;
    }

    public bool Contains(DictionaryEntryDetail item) => IndexOf(item) >= 0;

    public IEnumerator<DictionaryEntryDetail> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    public void CopyTo(DictionaryEntryDetail[] array, int arrayIndex) => throw new NotSupportedException();

    public void Add(DictionaryEntryDetail item) => throw new NotSupportedException();

    public void Clear() => throw new NotSupportedException();

    public void Insert(int index, DictionaryEntryDetail item) => throw new NotSupportedException();

    public bool Remove(DictionaryEntryDetail item) => throw new NotSupportedException();

    public void RemoveAt(int index) => throw new NotSupportedException();

    int IList.IndexOf(object? value) => value is DictionaryEntryDetail entry ? IndexOf(entry) : -1;

    bool IList.Contains(object? value) => value is DictionaryEntryDetail entry && Contains(entry);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    void ICollection.CopyTo(Array array, int index) => throw new NotSupportedException();

    int IList.Add(object? value) => throw new NotSupportedException();

    void IList.Insert(int index, object? value) => throw new NotSupportedException();

    void IList.Remove(object? value) => throw new NotSupportedException();
}
