using System;
using System.Collections;
using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models.Columnstore.Segment;

/// <summary>
/// Every row of a segment as an indexed list, built one row at a time as the grid asks for them
/// </summary>
/// <remarks>
/// The list is what a virtualizing grid needs and nothing more - a count and an indexer. Nothing is stored, so a
/// segment of any length costs only the rows on screen. Sorting and filtering have to stay off for the same reason,
/// both of them reading every row to do their work.
/// </remarks>
public sealed class SegmentRowList(SegmentRowContext context, int count) : IList<SegmentRowDetail>, IList
{
    public int Count { get; } = count;

    public bool IsReadOnly => true;

    public bool IsFixedSize => true;

    public bool IsSynchronized => false;

    public object SyncRoot { get; } = new();

    public SegmentRowDetail this[int index]
    {
        get => (uint)index < (uint)Count
            ? new SegmentRowDetail(context, index)
            : throw new ArgumentOutOfRangeException(nameof(index));
        set => throw new NotSupportedException();
    }

    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException();
    }

    public int IndexOf(SegmentRowDetail item) => item.Ordinal >= 0 && item.Ordinal < Count ? item.Ordinal : -1;

    public bool Contains(SegmentRowDetail item) => IndexOf(item) >= 0;

    public IEnumerator<SegmentRowDetail> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    public void CopyTo(SegmentRowDetail[] array, int arrayIndex) => throw new NotSupportedException();

    public void Add(SegmentRowDetail item) => throw new NotSupportedException();

    public void Clear() => throw new NotSupportedException();

    public void Insert(int index, SegmentRowDetail item) => throw new NotSupportedException();

    public bool Remove(SegmentRowDetail item) => throw new NotSupportedException();

    public void RemoveAt(int index) => throw new NotSupportedException();

    int IList.IndexOf(object? value) => value is SegmentRowDetail row ? IndexOf(row) : -1;

    bool IList.Contains(object? value) => value is SegmentRowDetail row && Contains(row);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    void ICollection.CopyTo(Array array, int index) => throw new NotSupportedException();

    int IList.Add(object? value) => throw new NotSupportedException();

    void IList.Insert(int index, object? value) => throw new NotSupportedException();

    void IList.Remove(object? value) => throw new NotSupportedException();
}
