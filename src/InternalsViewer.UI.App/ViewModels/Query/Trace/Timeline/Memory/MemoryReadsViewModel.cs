using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.Results;
using InternalsViewer.Query.Results.Enums;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed partial class MemoryReadsViewModel(PlayheadMemoryViewModel owner) : ObservableObject
{
    public const int PositionColumn = 0;

    public const int FunctionColumn = 1;

    public const int CallColumn = 2;

    public const int AddressColumn = 3;

    public const int LengthColumn = 4;

    public const int CountColumn = 5;

    private const int PositionWidth = 110;

    private const int FunctionWidth = 260;

    private const int CallWidth = 70;

    private const int AddressWidth = 200;

    private const int LengthWidth = 70;

    private const int CountWidth = 70;

    private const string HexPrefix = "0x";

    [ObservableProperty]
    private string _functionFilter = string.Empty;

    [ObservableProperty]
    private string _addressFilter = string.Empty;

    [ObservableProperty]
    private QueryResultSet? _table;

    [ObservableProperty]
    private ResultRow<long>? _selectedRow;

    private PlayheadMemoryViewModel Owner { get; } = owner;

    private int SortColumn { get; set; } = PositionColumn;

    private ResultSortDirection SortDirection { get; set; } = ResultSortDirection.Ascending;

    public void Load() => Rebuild();

    public void Clear()
    {
        SelectedRow = null;

        Table = null;
    }

    public void ClearFilters()
    {
        FunctionFilter = string.Empty;

        AddressFilter = string.Empty;
    }

    public void SetFilters(string? function, ulong? address)
    {
        FunctionFilter = function ?? string.Empty;

        AddressFilter = address is { } wanted ? $"{HexPrefix}{wanted:X16}" : string.Empty;
    }

    public void Sort(int ordinal, ResultSortDirection? direction)
    {
        SortColumn = direction is null ? PositionColumn : ordinal;

        SortDirection = direction ?? ResultSortDirection.Ascending;

        Rebuild();
    }

    public void FilterToFunction(int index)
    {
        if (GetRead(index) is { } read)
        {
            FunctionFilter = Owner.History.GetWriterLabel(read.CallNodeIndex);
        }
    }

    public void FilterToAddress(int index)
    {
        if (GetRead(index) is { } read)
        {
            AddressFilter = $"{HexPrefix}{Owner.History.Store.Blocks[Owner.Block].Address + (ulong)read.Offset:X16}";
        }
    }

    partial void OnFunctionFilterChanged(string value) => Rebuild();

    partial void OnAddressFilterChanged(string value) => Rebuild();

    partial void OnSelectedRowChanged(ResultRow<long>? value)
    {
        if (value is { Id: var index })
        {
            Owner.SelectRead((int)index);
        }
        else
        {
            Owner.ClearSelection();
        }
    }

    private void Rebuild()
    {
        SelectedRow = null;

        var columns = new List<ResultColumn>
        {
            SortableColumn(PositionColumn, "Position", PositionWidth),
            new(FunctionColumn, "Function", typeof(string), false) { Width = FunctionWidth },
            new(CallColumn, "Call", typeof(long), false) { Width = CallWidth, Alignment = ResultAlignment.Right },
            SortableColumn(AddressColumn, "Address", AddressWidth),
            new(LengthColumn, "Length", typeof(int), false) { Width = LengthWidth, Alignment = ResultAlignment.Right },
            SortableColumn(CountColumn, "Reads", CountWidth) with { Alignment = ResultAlignment.Right, IsStar = true }
        };

        if (!Owner.IsOpen)
        {
            Table = new QueryResultSet { Columns = columns, Rows = [] };

            return;
        }

        var store = Owner.History.Store;

        var block = store.Blocks[Owner.Block];

        var reads = store.GetReads(Owner.Block);

        var function = FunctionFilter.Trim();

        var address = ParseAddress(AddressFilter);

        var entries = new List<(int Index, ulong Start, int Count, ResultRow<long> Row)>();

        for (var index = 0; index < reads.Count; index++)
        {
            var read = reads[index];

            var label = Owner.History.GetWriterLabel(read.CallNodeIndex);

            if (function.Length > 0 && !label.Contains(function, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var start = block.Address + (ulong)read.Offset;

            if (address is { } wanted && (wanted < start || wanted >= start + (ulong)read.Length))
            {
                continue;
            }

            var readIndex = index;

            var row = new ResultRow<long>([
                new ActionLinks([new ActionLink(TracePositionText.GetHexText(read.Position), () => Owner.GoToRead(readIndex))]),
                new SwatchText(label, Owner.GetReadColour(read.CallNodeIndex).ToColor()),
                Owner.History.GetWriter(read.CallNodeIndex) is { } reader ? new CallValue(reader, read.CallIndex) : null,
                $"{HexPrefix}{start:X16}",
                read.Length,
                $"{read.Count:N0}"
            ])
            {
                Id = index
            };

            entries.Add((index, start, read.Count, row));
        }

        IEnumerable<(int Index, ulong Start, int Count, ResultRow<long> Row)> ordered = SortColumn switch
        {
            AddressColumn => entries.OrderBy(e => e.Start).ThenBy(e => e.Index),
            CountColumn => entries.OrderBy(e => e.Count).ThenBy(e => e.Index),
            _ => entries.OrderBy(e => e.Index)
        };

        if (SortDirection == ResultSortDirection.Descending)
        {
            ordered = ordered.Reverse();
        }

        Table = new QueryResultSet { Columns = columns, Rows = [.. ordered.Select(e => e.Row)] };
    }

    private ResultColumn SortableColumn(int ordinal, string name, int width)
        => new(ordinal, name, typeof(string), false)
        {
            Width = width,
            IsSortable = true,
            SortDirection = SortColumn == ordinal ? SortDirection : null
        };

    private MemoryHistoryRead? GetRead(int index)
    {
        if (!Owner.IsOpen)
        {
            return null;
        }

        var reads = Owner.History.Store.GetReads(Owner.Block);

        return index >= 0 && index < reads.Count ? reads[index] : null;
    }

    private static ulong? ParseAddress(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.StartsWith(HexPrefix, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[HexPrefix.Length..];
        }

        return ulong.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var address) ? address : null;
    }
}
