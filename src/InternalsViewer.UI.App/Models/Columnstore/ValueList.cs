using System;
using System.Collections;
using System.Collections.Generic;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.UI.App.Models.Columnstore;

/// <summary>
/// The values of one store by value page, indexed rather than materialised
/// </summary>
/// <remarks>
/// A page expands its whole payload on first read, so the values themselves are cheap once that has happened. The
/// list stays indexed anyway so a page of any length costs only the rows on screen.
/// </remarks>
public sealed class ValueList(SegmentValuePage page) : IList<ValueDetail>, IList
{
    public int Count { get; } = page.ValueCount;

    public bool IsReadOnly => true;

    public bool IsFixedSize => true;

    public bool IsSynchronized => false;

    public object SyncRoot { get; } = new();

    public ValueDetail this[int index]
    {
        get => (uint)index < (uint)Count ? new ValueDetail(page, index) : throw new ArgumentOutOfRangeException(nameof(index));
        set => throw new NotSupportedException();
    }

    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException();
    }

    public int IndexOf(ValueDetail item) => item.Index >= 0 && item.Index < Count ? item.Index : -1;

    int IList.IndexOf(object? value) => value is ValueDetail detail ? IndexOf(detail) : -1;

    public bool Contains(ValueDetail item) => IndexOf(item) >= 0;

    bool IList.Contains(object? value) => value is ValueDetail detail && Contains(detail);

    public IEnumerator<ValueDetail> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void CopyTo(ValueDetail[] array, int arrayIndex) => throw new NotSupportedException();

    void ICollection.CopyTo(Array array, int index) => throw new NotSupportedException();

    public void Add(ValueDetail item) => throw new NotSupportedException();

    int IList.Add(object? value) => throw new NotSupportedException();

    public void Clear() => throw new NotSupportedException();

    public void Insert(int index, ValueDetail item) => throw new NotSupportedException();

    void IList.Insert(int index, object? value) => throw new NotSupportedException();

    public bool Remove(ValueDetail item) => throw new NotSupportedException();

    void IList.Remove(object? value) => throw new NotSupportedException();

    public void RemoveAt(int index) => throw new NotSupportedException();
}
