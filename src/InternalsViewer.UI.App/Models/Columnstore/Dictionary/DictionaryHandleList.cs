using System;
using System.Collections;
using System.Collections.Generic;
using InternalsViewer.Internals.Columnstore.Dictionaries;

namespace InternalsViewer.UI.App.Models.Columnstore.Dictionary;

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

    public bool Contains(DictionaryHandleDetail item) => IndexOf(item) >= 0;

    public IEnumerator<DictionaryHandleDetail> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    public void CopyTo(DictionaryHandleDetail[] array, int arrayIndex) => throw new NotSupportedException();

    public void Add(DictionaryHandleDetail item) => throw new NotSupportedException();

    public void Clear() => throw new NotSupportedException();

    public void Insert(int index, DictionaryHandleDetail item) => throw new NotSupportedException();

    public bool Remove(DictionaryHandleDetail item) => throw new NotSupportedException();

    public void RemoveAt(int index) => throw new NotSupportedException();

    int IList.IndexOf(object? value) => value is DictionaryHandleDetail handle ? IndexOf(handle) : -1;

    bool IList.Contains(object? value) => value is DictionaryHandleDetail handle && Contains(handle);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    void ICollection.CopyTo(Array array, int index) => throw new NotSupportedException();

    int IList.Add(object? value) => throw new NotSupportedException();

    void IList.Insert(int index, object? value) => throw new NotSupportedException();

    void IList.Remove(object? value) => throw new NotSupportedException();
}
