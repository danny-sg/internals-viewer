using System;
using System.Collections;
using System.Collections.Generic;
using InternalsViewer.Internals.Columnstore.Dictionaries;

namespace InternalsViewer.UI.App.Models.Columnstore.Dictionary;

/// <summary>
/// One handle, being where a string dictionary keeps the value an entry stands for
/// </summary>
/// <remarks>
/// A string is not fixed width, so it cannot be indexed the way a numeric dictionary indexes its value array.
/// The handle names the page holding the value and where it starts on that page, which is what the lookup follows.
/// </remarks>
public sealed class DictionaryHandleDetail(StringDictionary dictionary, int index) : IEquatable<DictionaryHandleDetail>
{
    public int Index { get; } = index;

    public long DataId => dictionary.FirstId + Index;

    public int Page => dictionary.Handles[Index].Page;

    /// <summary>
    /// Where the value starts on its page, counted in bits on a Huffman coded page and bytes on a plain one
    /// </summary>
    public int Offset => dictionary.Handles[Index].Offset;

    public int Length => StoredBytes?.Length ?? 0;

    /// <summary>
    /// Where the handle itself sits in the blob, the array running from a fixed place after the header
    /// </summary>
    public int HandleOffset => StringDictionary.HandleArrayOffset + (Index * dictionary.HandleSize);

    public string HandleOffsetDescription => $"0x{HandleOffset:X}";

    public bool IsLobPointer => dictionary.TryGetLobPointer(Index, out _);

    /// <summary>
    /// What the handle leads to, a value too big for the store leading to a pointer rather than the value
    /// </summary>
    public string Description => dictionary.TryGetLobPointer(Index, out var pointer)
        ? $"LOB {pointer.PageAddress} slot {pointer.Slot}, {pointer.Length} bytes"
        : string.Empty;

    private byte[]? StoredBytes => field ??= dictionary.GetValueBytesAt(Index);

    public bool Equals(DictionaryHandleDetail? other) => other is not null && other.Index == Index;

    public override bool Equals(object? obj) => Equals(obj as DictionaryHandleDetail);

    public override int GetHashCode() => Index;
}

/// <summary>
/// The handles of a string dictionary, indexed rather than materialised
/// </summary>
public sealed class DictionaryHandleList(StringDictionary dictionary) : IList<DictionaryHandleDetail>, IList
{
    public int Count { get; } = dictionary.Handles.Length;

    public bool IsReadOnly => true;

    public bool IsFixedSize => true;

    public bool IsSynchronized => false;

    public object SyncRoot { get; } = new();

    public DictionaryHandleDetail this[int index]
    {
        get => (uint)index < (uint)Count
            ? new DictionaryHandleDetail(dictionary, index)
            : throw new ArgumentOutOfRangeException(nameof(index));
        set => throw new NotSupportedException();
    }

    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException();
    }

    public int IndexOf(DictionaryHandleDetail item) => item.Index >= 0 && item.Index < Count ? item.Index : -1;

    int IList.IndexOf(object? value) => value is DictionaryHandleDetail handle ? IndexOf(handle) : -1;

    public bool Contains(DictionaryHandleDetail item) => IndexOf(item) >= 0;

    bool IList.Contains(object? value) => value is DictionaryHandleDetail handle && Contains(handle);

    public IEnumerator<DictionaryHandleDetail> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void CopyTo(DictionaryHandleDetail[] array, int arrayIndex) => throw new NotSupportedException();

    void ICollection.CopyTo(Array array, int index) => throw new NotSupportedException();

    public void Add(DictionaryHandleDetail item) => throw new NotSupportedException();

    int IList.Add(object? value) => throw new NotSupportedException();

    public void Clear() => throw new NotSupportedException();

    public void Insert(int index, DictionaryHandleDetail item) => throw new NotSupportedException();

    void IList.Insert(int index, object? value) => throw new NotSupportedException();

    public bool Remove(DictionaryHandleDetail item) => throw new NotSupportedException();

    void IList.Remove(object? value) => throw new NotSupportedException();

    public void RemoveAt(int index) => throw new NotSupportedException();
}
