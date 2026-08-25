using System;
using System.Collections;
using System.Collections.Generic;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.UI.App.Models.Columnstore.Segment;

/// <summary>
/// The units of a bit pack array, indexed rather than materialised
/// </summary>
/// <remarks>
/// A segment carries hundreds of thousands of units, so a row is built only when the table asks for one and the
/// list itself costs nothing beyond the blob it reads from.
/// </remarks>
public sealed class BitpackUnitList(SegmentBlob blob) : IList<BitpackUnitRow>, IList
{
    public int Count { get; } = blob.Header.BitpackUnitCount;

    public bool IsReadOnly => true;

    public bool IsFixedSize => true;

    public bool IsSynchronized => false;

    public object SyncRoot { get; } = new();

    public BitpackUnitRow this[int index]
    {
        get => (uint)index < (uint)Count
            ? new BitpackUnitRow(blob, index)
            : throw new ArgumentOutOfRangeException(nameof(index));
        set => throw new NotSupportedException();
    }

    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException();
    }

    public int IndexOf(BitpackUnitRow item) => item.Unit >= 0 && item.Unit < Count ? item.Unit : -1;

    public bool Contains(BitpackUnitRow item) => IndexOf(item) >= 0;

    public IEnumerator<BitpackUnitRow> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    public void CopyTo(BitpackUnitRow[] array, int arrayIndex) => throw new NotSupportedException();

    public void Add(BitpackUnitRow item) => throw new NotSupportedException();

    public void Clear() => throw new NotSupportedException();

    public void Insert(int index, BitpackUnitRow item) => throw new NotSupportedException();

    public bool Remove(BitpackUnitRow item) => throw new NotSupportedException();

    public void RemoveAt(int index) => throw new NotSupportedException();

    int IList.IndexOf(object? value) => value is BitpackUnitRow row ? IndexOf(row) : -1;

    bool IList.Contains(object? value) => value is BitpackUnitRow row && Contains(row);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    void ICollection.CopyTo(Array array, int index) => throw new NotSupportedException();

    int IList.Add(object? value) => throw new NotSupportedException();

    void IList.Insert(int index, object? value) => throw new NotSupportedException();

    void IList.Remove(object? value) => throw new NotSupportedException();
}
