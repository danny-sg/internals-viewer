using System;
using System.Collections.Generic;
using InternalsViewer.Query.Results;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

internal sealed class CachedRowSource<T>(IReadOnlyList<T> items, int fieldCount, Func<T, int, object?[]> values) : IResultRowSource
{
    private const int CachedRows = 512;

    public int FieldCount { get; } = fieldCount;

    private Dictionary<int, object?[]> Cache { get; } = new();

    private Queue<int> Cached { get; } = new();

    public object? Value(int row, int ordinal)
    {
        if (!Cache.TryGetValue(row, out var rowValues))
        {
            rowValues = values(items[row], row);

            Cache[row] = rowValues;

            Cached.Enqueue(row);

            if (Cached.Count > CachedRows)
            {
                Cache.Remove(Cached.Dequeue());
            }
        }

        return rowValues[ordinal];
    }

    public void Invalidate()
    {
        Cache.Clear();

        Cached.Clear();
    }
}
