using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Controls.HexView;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed partial class MemoryWritesViewModel(PlayheadMemoryViewModel owner) : ObservableObject
{
    public const int PositionColumn = 0;

    public const int FunctionColumn = 1;

    private const int PositionWidth = 110;

    private const int FunctionWidth = 260;

    private const int AddressWidth = 170;

    private const int LengthWidth = 70;

    private const int ValueWidth = 220;

    private const int PinWidth = 50;

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

    partial void OnFunctionFilterChanged(string value) => Rebuild();

    partial void OnAddressFilterChanged(string value) => Rebuild();

    partial void OnSelectedRowChanged(ResultRow<long>? value)
    {
        if (value is { Id: var index })
        {
            Owner.SelectWrite((int)index);
        }
    }

    private void Rebuild()
    {
        var columns = new List<ResultColumn>
        {
            new(0, "Position", typeof(string), false) { Width = PositionWidth },
            new(1, "Function", typeof(string), false) { Width = FunctionWidth },
            new(2, "Address", typeof(string), false) { Width = AddressWidth },
            new(3, "Length", typeof(int), false) { Width = LengthWidth, Alignment = ResultAlignment.Right },
            new(4, "Value", typeof(string), false) { Width = ValueWidth },
            new(5, string.Empty, typeof(string), false) { Width = PinWidth, Alignment = ResultAlignment.Center }
        };

        if (!Owner.IsOpen)
        {
            Table = new QueryResultSet { Columns = columns, Rows = [] };

            return;
        }

        var store = Owner.History.Store;

        var block = store.Blocks[Owner.Block];

        var writes = store.GetWrites(Owner.Block);

        var function = FunctionFilter.Trim();

        var address = ParseAddress(AddressFilter);

        var rows = new List<ResultRow<long>>();

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

            rows.Add(new ResultRow<long>([
                new ActionLinks([new ActionLink(TracePositionText.GetHexText(write.Position), () => Owner.GoToWrite(writeIndex))]),
                new SwatchText(label, Owner.GetCategoryColour(write.CallNodeIndex).ToColor()),
                $"{HexPrefix}{start:X16}",
                (int)write.Length,
                GetValueText(store.GetBytes(write)),
                new ActionLinks([new ActionLink("Pin", () => Owner.PinRange(write.Offset, write.Length)) { Glyph = HexTipPin.GetGlyph(false) }])
            ])
            {
                Id = index
            });
        }

        Table = new QueryResultSet { Columns = columns, Rows = rows };
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

    private static string GetValueText(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > ValueBytesShown)
        {
            return $"{bytes.Length:N0} Bytes";
        }

        var text = new StringBuilder(bytes.Length * 3);

        foreach (var value in bytes)
        {
            if (text.Length > 0)
            {
                text.Append(' ');
            }

            text.Append(value.ToString("X2"));
        }

        return text.ToString();
    }
}
