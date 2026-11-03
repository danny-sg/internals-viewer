using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings.Enums;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory.Enums;
using Microsoft.UI;
using Colour = System.Drawing.Color;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed partial class MemoryBindingsViewModel : ObservableObject
{
    private const int HueWheel = 256;

    private const int FieldSaturation = 60;

    private const int FieldValue = 250;

    private const string NotBound = "No Bindings At This Position";

    private const string VariablePrefix = "Variable ";

    private const string StatusSuffix = " Status";

    private const string LengthSuffix = " Length";

    private const string ValueSuffix = " Value";

    private static readonly int FieldCount = Enum.GetValues<ColumnBindingField>().Length;

    private static readonly (ColumnBindingFlags Flag, string Name)[] FlagNames =
    [
        (ColumnBindingFlags.Length, "Length"),
        (ColumnBindingFlags.ByReference, "By Reference"),
        (ColumnBindingFlags.LargeObject, "Large Object")
    ];

    private static readonly Dictionary<ColumnBindingField, string> FieldNames = new()
    {
        [ColumnBindingField.Ordinal] = "Ordinal",
        [ColumnBindingField.Type] = "Type",
        [ColumnBindingField.MaximumLength] = "Size",
        [ColumnBindingField.RecordOffset] = "Record Offset",
        [ColumnBindingField.StatusOffset] = "Status Offset",
        [ColumnBindingField.LengthOffset] = "Length Offset",
        [ColumnBindingField.ValueOffset] = "Value Offset",
        [ColumnBindingField.Flags] = "Flags",
        [ColumnBindingField.ColumnAttributes] = "Column Attributes",
        [ColumnBindingField.InfrequentData] = "Infrequent Data"
    };

    [ObservableProperty]
    private bool _isAvailable;

    [ObservableProperty]
    private bool _isTableVisible;

    [ObservableProperty]
    private QueryResultSet? _table;

    [ObservableProperty]
    private ResultRow<long>? _selectedRow;

    [ObservableProperty]
    private string _columnCount = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBindingsLinkVisible))]
    private string _bindingsAddress = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsColumnsLinkVisible))]
    private string _columnsAddress = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRowBufferLinkVisible))]
    private string _rowBufferAddress = string.Empty;

    [ObservableProperty]
    private string _emptyText = string.Empty;

    public ObservableCollection<Marker>? Markers { get; private set; }

    public bool IsBindingsLinkVisible => BindingsAddress.Length > 0;

    public bool IsColumnsLinkVisible => ColumnsAddress.Length > 0;

    public bool IsRowBufferLinkVisible => RowBufferAddress.Length > 0;

    private PlayheadMemoryViewModel? Owner { get; set; }

    private MemoryBindingsKind Kind { get; set; }

    private ulong Address { get; set; }

    private int Size { get; set; }

    private ScanBindingReference? Scan { get; set; }

    private ulong Columns { get; set; }

    private List<ColumnBinding> Bound { get; set; } = [];

    private MemoryHistoryReader? Reader { get; set; }

    public void Load(PlayheadMemoryViewModel owner)
    {
        Clear();

        var history = owner.History;

        var block = history.Store.Blocks[owner.Block];

        var scan = FindScan(history, block, owner.Position);

        Kind = history.IsAccessorBindings(owner.Block) ? MemoryBindingsKind.Bindings
               : history.IsColumnBindings(owner.Block) ? MemoryBindingsKind.Columns
               : scan?.RowBuffer == block.Address ? MemoryBindingsKind.RowBuffer
               : MemoryBindingsKind.None;

        if (Kind == MemoryBindingsKind.None)
        {
            return;
        }

        Owner = owner;

        Scan = scan;

        Address = block.Address;

        Size = block.Size;

        BindingsAddress = Kind != MemoryBindingsKind.Bindings && scan is { } owned ? GetAddressText(owned.Bindings) : string.Empty;

        RowBufferAddress = Kind == MemoryBindingsKind.Columns && scan is { RowBuffer: not 0 } buffered
            ? GetAddressText(buffered.RowBuffer)
            : string.Empty;

        IsTableVisible = Kind != MemoryBindingsKind.Bindings;

        IsAvailable = true;
    }

    public void Clear()
    {
        Owner = null;

        Kind = MemoryBindingsKind.None;

        Scan = null;

        Reader = null;

        Columns = 0;

        Bound = [];

        Markers = null;

        Table = null;

        ColumnCount = string.Empty;

        BindingsAddress = string.Empty;

        ColumnsAddress = string.Empty;

        RowBufferAddress = string.Empty;

        EmptyText = string.Empty;

        IsTableVisible = false;

        IsAvailable = false;
    }

    public void Refresh(TracePosition position)
    {
        if (!IsAvailable || Owner is not { } owner || !position.IsKnown)
        {
            return;
        }

        if (Reader?.HasChanged(position) == false)
        {
            return;
        }

        var reader = new MemoryHistoryReader(owner.History.Store, position);

        var (count, columns) = GetColumns(reader);

        var bound = columns == 0 || Kind == MemoryBindingsKind.Bindings ? [] : AccessorBindingLayout.ReadColumns(reader, columns, count);

        Reader = columns != 0 ? reader : null;

        Columns = columns;

        ColumnCount = columns == 0 ? string.Empty : $"{count}";

        ColumnsAddress = Kind != MemoryBindingsKind.Columns && columns != 0 ? GetAddressText(columns) : string.Empty;

        EmptyText = IsTableVisible && bound.Count == 0 ? NotBound : string.Empty;

        if (Markers is not null && bound.SequenceEqual(Bound))
        {
            return;
        }

        Bound = bound;

        Table = IsTableVisible ? GetTable(owner, bound, position) : null;

        Markers = GetMarkers(null);
    }

    public void OpenBindings() => Open(Scan?.Bindings ?? 0);

    public void OpenColumns() => Open(Columns);

    public void OpenRowBuffer() => Open(Scan?.RowBuffer ?? 0);

    partial void OnSelectedRowChanged(ResultRow<long>? value)
    {
        if (Owner is not { } owner || Kind == MemoryBindingsKind.Bindings)
        {
            return;
        }

        int? selected = value is { Id: var id } && id >= 0 && id < Bound.Count ? (int)id : null;

        Markers = GetMarkers(selected);

        owner.ShowMarkers(selected is { } index ? GetStart(Bound[index]) : null);
    }

    private (int Count, ulong Columns) GetColumns(MemoryHistoryReader reader)
        => Kind switch
        {
            MemoryBindingsKind.Columns => (AccessorBindingLayout.GetColumnCount(Size), Address),
            MemoryBindingsKind.Bindings => AccessorBindingLayout.ReadHeader(reader, Address) ?? (0, 0UL),
            _ => Scan is { } scan ? AccessorBindingLayout.ReadHeader(reader, scan.Bindings) ?? (0, 0UL) : (0, 0UL)
        };

    private void Open(ulong address)
    {
        if (address != 0)
        {
            Owner?.RequestMemory(address);
        }
    }

    private int GetStart(ColumnBinding column)
        => Kind == MemoryBindingsKind.Columns
            ? column.Index * AccessorBindingLayout.ColumnSize
            : Math.Min(column.StatusOffset, column.ValueOffset);

    private ObservableCollection<Marker> GetMarkers(int? selected)
    {
        var markers = new List<Marker>();

        if (Kind == MemoryBindingsKind.Bindings)
        {
            var fields = AccessorBindingLayout.Fields;

            for (var index = 0; index < fields.Count; index++)
            {
                var colour = ColourHelpers.HsvToColor(index * HueWheel / fields.Count, FieldSaturation, FieldValue);

                AddMarker(markers, fields[index].Name, fields[index].Offset, fields[index].Length, colour);
            }
        }

        if (selected is { } row && row < Bound.Count)
        {
            if (Kind == MemoryBindingsKind.Columns)
            {
                AddColumnFields(markers, Bound[row]);
            }
            else if (Kind == MemoryBindingsKind.RowBuffer)
            {
                AddRowBufferFields(markers, Bound[row]);
            }
        }

        return new ObservableCollection<Marker>(markers.OrderBy(m => m.StartPosition));
    }

    private void AddColumnFields(List<Marker> markers, ColumnBinding column)
    {
        var name = GetColumnName(column);

        var start = column.Index * AccessorBindingLayout.ColumnSize;

        var infrequent = column.InfrequentData >= Address && column.InfrequentData - Address < (ulong)Size
            ? (int)(column.InfrequentData - Address)
            : -1;

        foreach (var field in AccessorBindingLayout.ColumnFields)
        {
            var offset = field.IsInfrequent ? infrequent < 0 ? -1 : infrequent + field.Offset : start + field.Offset;

            AddMarker(markers, $"{name} {FieldNames[field.Field]}", offset, field.Length, GetFieldColour(field.Field));
        }
    }

    private void AddRowBufferFields(List<Marker> markers, ColumnBinding column)
    {
        var name = GetColumnName(column);

        AddMarker(markers,
                  name + StatusSuffix,
                  column.StatusOffset,
                  AccessorBindingLayout.StatusSize,
                  GetFieldColour(ColumnBindingField.StatusOffset));

        if (column.LengthOffset is { } lengthOffset)
        {
            AddMarker(markers,
                      name + LengthSuffix,
                      lengthOffset,
                      AccessorBindingLayout.LengthSize,
                      GetFieldColour(ColumnBindingField.LengthOffset));
        }

        AddMarker(markers, name + ValueSuffix, column.ValueOffset, column.ValueLength, GetFieldColour(ColumnBindingField.ValueOffset));
    }

    private void AddMarker(List<Marker> markers, string name, int offset, int length, Colour colour)
    {
        if (offset < 0 || offset >= Size || length <= 0)
        {
            return;
        }

        markers.Add(new Marker
        {
            Name = name,
            StartPosition = offset,
            EndPosition = Math.Min(offset + length, Size) - 1,
            BackColour = colour.ToWindowsColor(),
            MapColour = colour.ToWindowsColor(),
            ForeColour = Colors.Black
        });
    }

    private static ScanBindingReference? FindScan(MemoryHistoryViewModel history, MemoryHistoryBlock block, TracePosition position)
    {
        if (block.Iterator == 0 || block.IsIterator)
        {
            return null;
        }

        var store = history.Store;

        foreach (var at in GetScanPositions(store, block, position))
        {
            if (AccessorBindingLayout.ReadScan(new MemoryHistoryReader(store, at), block.Iterator) is { } scan
                && scan.Contains(block.Address)
                && store.GetBlock(scan.Bindings, at) is >= 0 and var bindings
                && history.IsAccessorBindings(bindings))
            {
                return scan;
            }
        }

        return null;
    }

    private static IEnumerable<TracePosition> GetScanPositions(MemoryHistoryStore store, MemoryHistoryBlock block, TracePosition position)
    {
        if (position.IsKnown)
        {
            yield return position;
        }

        var iterator = store.GetBlock(block.Iterator, block.FromPosition);

        if (iterator >= 0 && store.Blocks[iterator] is { WriteCount: > 0 } found)
        {
            yield return store.GetPoint(found.FirstWrite + found.WriteCount - 1).Position;
        }
    }

    private QueryResultSet GetTable(PlayheadMemoryViewModel owner, List<ColumnBinding> bound, TracePosition position)
    {
        var columns = new List<ResultColumn>
        {
            GetColumn(ColumnBindingField.Ordinal, typeof(int), ResultAlignment.Right),
            GetColumn(ColumnBindingField.Type, typeof(string)),
            GetColumn(ColumnBindingField.MaximumLength, typeof(int), ResultAlignment.Right),
            GetColumn(ColumnBindingField.RecordOffset, typeof(string), ResultAlignment.Right),
            GetColumn(ColumnBindingField.StatusOffset, typeof(string), ResultAlignment.Right),
            GetColumn(ColumnBindingField.LengthOffset, typeof(string), ResultAlignment.Right),
            GetColumn(ColumnBindingField.ValueOffset, typeof(string), ResultAlignment.Right),
            GetColumn(ColumnBindingField.Flags, typeof(string)),
            GetColumn(ColumnBindingField.ColumnAttributes, typeof(string))
        };

        var rows = bound.Select((b, i) => new ResultRow<long>([b.Ordinal,
                                                               GetTypeText(b.Type),
                                                               b.MaximumLength,
                                                               GetRecordText(b),
                                                               GetOffsetText(b.StatusOffset),
                                                               b.LengthOffset is { } length ? GetOffsetText(length) : null,
                                                               GetOffsetText(b.ValueOffset),
                                                               GetFlagsCell(b.Flags),
                                                               GetPointerCell(owner, b.ColumnAttributes, position)])
        {
            Id = i
        });

        return new QueryResultSet { Columns = columns, Rows = [.. rows] };
    }

    private ResultColumn GetColumn(ColumnBindingField field, Type type, ResultAlignment alignment = ResultAlignment.Left)
        => new((int)field, FieldNames[field], type, true)
        {
            Alignment = alignment,
            MarkerColour = IsMarked(field) ? GetFieldColour(field) : null
        };

    private bool IsMarked(ColumnBindingField field)
        => Kind == MemoryBindingsKind.Columns
            ? field != ColumnBindingField.InfrequentData
            : field is ColumnBindingField.StatusOffset or ColumnBindingField.LengthOffset or ColumnBindingField.ValueOffset;

    private static object? GetPointerCell(PlayheadMemoryViewModel owner, ulong address, TracePosition position)
    {
        if (address == 0)
        {
            return null;
        }

        var text = GetAddressText(address);

        return owner.History.GetBlock(address, position) < 0
            ? text
            : new ActionLinks([new ActionLink(text, () => owner.RequestMemory(address))])
            {
                Suffix = owner.History.GetPointerTarget(address, position)
            };
    }

    private static BadgeText GetFlagsCell(ColumnBindingFlags flags)
        => new([], $"0x{(uint)flags:X}", [.. FlagNames.Where(f => (flags & f.Flag) != 0).Select(f => f.Name)]);

    private static string GetColumnName(ColumnBinding column) => $"Column {column.Ordinal}";

    private static string GetRecordText(ColumnBinding column)
        => column.IsVariableLength ? $"{VariablePrefix}{column.VariableIndex}" : GetOffsetText(column.RecordOffset);

    private static string GetTypeText(byte type) => SqlTypeHelpers.TryGetSqlType(type, out var sqlType) ? $"{sqlType}" : $"0x{type:X2}";

    private static string GetOffsetText(int offset) => $"0x{offset:X}";

    private static string GetAddressText(ulong address) => $"0x{address:X16}";

    private static Colour GetFieldColour(ColumnBindingField field)
        => ColourHelpers.HsvToColor((int)field * HueWheel / FieldCount, FieldSaturation, FieldValue);
}
