using System;
using System.Collections.Generic;
using System.Data;
using InternalsViewer.Query.FullTrace.Memory.History.Batches;
using InternalsViewer.Query.Results;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

internal sealed class DeepDataRowSource(List<DeepDataEntry> entries, ulong blockAddress, Func<SqlDbType?> getType) : IResultRowSource
{
    public List<DeepDataEntry> Entries { get; private set; } = entries;

    public int FieldCount => 4;

    public void SetEntries(List<DeepDataEntry> value) => Entries = value;

    public object? Value(int row, int ordinal)
    {
        var entry = Entries[row];

        return ordinal switch
        {
            0 => row,
            1 => $"0x{entry.Address - blockAddress:X}",
            2 => entry.Length,
            _ => BatchSlotFormatter.FormatDeep(entry.Data, getType())
        };
    }
}
