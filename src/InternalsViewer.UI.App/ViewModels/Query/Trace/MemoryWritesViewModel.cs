using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Converters.Decoder;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.Results;
using InternalsViewer.Query.Results.Enums;
using InternalsViewer.UI.App.Controls.HexView;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed partial class MemoryWritesViewModel(PlayheadMemoryViewModel owner) : ObservableObject
{
    public const int PositionColumn = 0;

    public const int FunctionColumn = 1;

    public const int AddressColumn = 2;

    public const int LengthColumn = 3;

    public const int ValueColumn = 4;

    private const int PositionWidth = 110;

    private const int FunctionWidth = 260;

    private const int AddressWidth = 200;

    private const int LengthWidth = 70;

    private const int ValueWidth = 220;

    private const int ValueBytesShown = 16;

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

    private List<MemoryHistoryWrite> VisibleWrites { get; set; } = [];

    private List<int> VisibleIndexes { get; set; } = [];

    public void Load() => Rebuild();

    public void Step(bool isForward, TracePosition position)
    {
        if (!Owner.IsOpen || Table is not { } table)
        {
            return;
        }

        var step = isForward
            ? MemoryChangeNavigation.GetNextWrite(VisibleWrites, position)
            : MemoryChangeNavigation.GetPreviousWrite(VisibleWrites, position);

        if (step < 0)
        {
            return;
        }

        var index = VisibleIndexes[step];

        SelectedRow = table.Rows.FirstOrDefault(r => r.Id == index);
    }

    public void Sort(int ordinal, ResultSortDirection? direction)
    {
        SortColumn = direction is null ? PositionColumn : ordinal;

        SortDirection = direction ?? ResultSortDirection.Ascending;

        Rebuild();
    }

    public void FilterToFunction(int index)
    {
        if (GetWrite(index) is { } write)
        {
            FunctionFilter = Owner.History.GetWriterLabel(write.CallNodeIndex);
        }
    }

    public void FilterToAddress(int index)
    {
        if (GetWrite(index) is { } write)
        {
            AddressFilter = $"{HexPrefix}{Owner.History.Store.Blocks[Owner.Block].Address + (ulong)write.Offset:X16}";
        }
    }

    public void Clear()
    {
        SelectedRow = null;

        Table = null;

        VisibleWrites = [];

        VisibleIndexes = [];
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

    partial void OnFunctionFilterChanged(string value) => Rebuild();

    partial void OnAddressFilterChanged(string value) => Rebuild();

    partial void OnSelectedRowChanged(ResultRow<long>? value)
    {
        if (value is { Id: var index })
        {
            Owner.SelectWrite((int)index);
        }
        else
        {
            Owner.ClearWriteSelection();
        }
    }

    private void Rebuild()
    {
        SelectedRow = null;

        var columns = new List<ResultColumn>
        {
            SortableColumn(PositionColumn, "Position", PositionWidth),
            new(FunctionColumn, "Function", typeof(string), false) { Width = FunctionWidth },
            SortableColumn(AddressColumn, "Address", AddressWidth),
            new(LengthColumn, "Length", typeof(int), false) { Width = LengthWidth, Alignment = ResultAlignment.Right },
            SortableColumn(ValueColumn, "Value", ValueWidth)
        };

        if (!Owner.IsOpen)
        {
            VisibleWrites = [];

            VisibleIndexes = [];

            Table = new QueryResultSet { Columns = columns, Rows = [] };

            return;
        }

        var store = Owner.History.Store;

        var block = store.Blocks[Owner.Block];

        var writes = store.GetWrites(Owner.Block);

        var function = FunctionFilter.Trim();

        var address = ParseAddress(AddressFilter);

        var entries = new List<(int Index, ulong Start, string Value, ResultRow<long> Row)>();

        for (var index = 0; index < writes.Count; index++)
        {
            var write = writes[index];

            var label = Owner.History.GetWriterLabel(write.CallNodeIndex);

            if (function.Length > 0 && !label.Contains(function, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var start = block.Address + (ulong)write.Offset;

            if (address is { } wanted && (wanted < start || wanted >= start + write.Length))
            {
                continue;
            }

            var writeIndex = index;

            var value = GetValueText(write, store.GetBytes(write));

            var row = new ResultRow<long>([
                new ActionLinks([new ActionLink(TracePositionText.GetHexText(write.Position), () => Owner.GoToWrite(writeIndex))]),
                new SwatchText(label, Owner.GetCategoryColour(write.CallNodeIndex).ToColor()),
                new ActionText($"{HexPrefix}{start:X16}",
                               [new ActionLink("Pin", () => Owner.PinRange(write.Offset, write.Length)) { Glyph = HexTipPin.GetGlyph(false) }]),
                (int)write.Length,
                value
            ])
            {
                Id = index
            };

            entries.Add((index, start, value, row));
        }

        VisibleWrites = [.. entries.Select(e => writes[e.Index])];

        VisibleIndexes = [.. entries.Select(e => e.Index)];

        IEnumerable<(int Index, ulong Start, string Value, ResultRow<long> Row)> ordered = SortColumn switch
        {
            AddressColumn => entries.OrderBy(e => e.Start).ThenBy(e => e.Index),
            ValueColumn => entries.OrderBy(e => e.Value, StringComparer.Ordinal).ThenBy(e => e.Index),
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

    private MemoryHistoryWrite? GetWrite(int index)
    {
        if (!Owner.IsOpen)
        {
            return null;
        }

        var writes = Owner.History.Store.GetWrites(Owner.Block);

        return index >= 0 && index < writes.Count ? writes[index] : null;
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

    private string GetValueText(MemoryHistoryWrite write, ReadOnlySpan<byte> bytes)
    {
        if (GetTypedWatch(write) is { DataType: { } type }
            && DataDecoder.Decode(bytes.ToArray()).Find(r => r.DataType == type) is { } decoded)
        {
            return decoded.Value;
        }

        return bytes.Length > ValueBytesShown ? $"{bytes.Length:N0} Bytes" : $"{HexPrefix}{Convert.ToHexString(bytes)}";
    }

    private HexWatch? GetTypedWatch(MemoryHistoryWrite write)
    {
        foreach (var watch in Owner.Watches ?? [])
        {
            if (watch.DataType is not null && watch.Offset == write.Offset && watch.Length == write.Length)
            {
                return watch;
            }
        }

        return null;
    }
}
