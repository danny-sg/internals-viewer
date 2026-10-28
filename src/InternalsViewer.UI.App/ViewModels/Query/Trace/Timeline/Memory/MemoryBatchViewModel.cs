using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.History.Batches;
using InternalsViewer.Query.FullTrace.Memory.History.Batches.Enums;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Logging;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using Microsoft.UI;
using Colour = System.Drawing.Color;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed partial class MemoryBatchViewModel : ObservableObject
{
    public const int VectorValueColumn = 3;

    private const int LoadInterval = 120;

    private const int RowWidth = 70;

    private const int SelectionWidth = 90;

    private const int OffsetWidth = 100;

    private const int FieldWidth = 180;

    private const int VectorWidth = 170;

    private const int TypedVectorWidth = 240;

    private const int BitGroup = 8;

    private const int SelectionEntrySize = sizeof(ushort);

    private const int SlotSize = sizeof(ulong);

    private const int HueWheel = 256;

    private const int FieldSaturation = 60;

    private const int FieldValue = 250;

    private const string NoBatch = "No Batch At This Position";

    private const string NoVector = "Not In A Batch At This Position";

    private const string NotAttached = "Not Attached At This Position";

    private const string PureBadge = "Pure";

    private static readonly Colour OpenedVectorColour = Colour.FromArgb(40, Colour.Gold);

    private static readonly Colour PureColour = Colour.FromArgb(255, 29, 158, 117);

    private static readonly List<Colour> FieldColours =
    [
        .. BatchReader.Fields.Select((_, i) => ColourHelpers.HsvToColor(i * HueWheel / BatchReader.Fields.Count,
                                                                        FieldSaturation,
                                                                        FieldValue))
    ];

    [ObservableProperty]
    private bool _isAvailable;

    [ObservableProperty]
    private bool _isVectorsAvailable;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private BatchPartType _partType;

    [ObservableProperty]
    private QueryResultSet? _headerTable;

    [ObservableProperty]
    private ResultRow<long>? _selectedHeaderRow;

    [ObservableProperty]
    private QueryResultSet? _table;

    [ObservableProperty]
    private int _version;

    [ObservableProperty]
    private QueryResultSet? _partTable;

    [ObservableProperty]
    private int _partVersion;

    [ObservableProperty]
    private ResultRow<long>? _selectedPartRow;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _vectors = string.Empty;

    [ObservableProperty]
    private string _capacity = string.Empty;

    [ObservableProperty]
    private string _activeRows = string.Empty;

    [ObservableProperty]
    private string _vectorName = string.Empty;

    [ObservableProperty]
    private string _nextPage = string.Empty;

    [ObservableProperty]
    private bool _isNextPageVisible;

    [ObservableProperty]
    private string _bits = string.Empty;

    [ObservableProperty]
    private string _bitmapValue = string.Empty;

    [ObservableProperty]
    private string _emptyText = string.Empty;

    public MemoryBatchViewModel()
    {
        Loader = new PlayheadLoader<BatchSnapshot?>(TimeSpan.FromMilliseconds(LoadInterval), ApplySnapshot, b => IsLoading = b);
    }

    public ObservableCollection<Marker>? FieldMarkers { get; private set; }

    public bool IsHeader => IsAvailable && PartType == BatchPartType.Header;

    public bool IsBatchLinkVisible => IsAvailable && PartType != BatchPartType.Header;

    public bool IsBitmap => IsAvailable && PartType is BatchPartType.AllocationBitmap or BatchPartType.PureBitmap;

    public bool IsVector => IsAvailable && PartType == BatchPartType.Vector;

    public bool IsDeepData => IsAvailable && PartType == BatchPartType.DeepData;

    public bool IsVectorNameVisible => IsVector || IsDeepData;

    public bool IsPartTableVisible
        => IsAvailable
           && PartType is BatchPartType.VectorArray or BatchPartType.Vector or BatchPartType.Selection or BatchPartType.DeepData;

    private PlayheadMemoryViewModel? Owner { get; set; }

    private MemoryHistoryBatches? Batches { get; set; }

    private int Block { get; set; } = -1;

    private ulong BlockAddress { get; set; }

    private BatchSnapshot? Snapshot { get; set; }

    private MemoryHistoryBatch? Current => Snapshot?.Batch;

    private PlayheadLoader<BatchSnapshot?> Loader { get; }

    private string HeaderKey { get; set; } = string.Empty;

    private ulong NextPageAddress { get; set; }

    private BatchRowSource? Source { get; set; }

    private IResultRowSource? PartSource { get; set; }

    public void Load(PlayheadMemoryViewModel owner)
    {
        Clear();

        Owner = owner;

        Batches = owner.History.Batches;

        Block = owner.Block;

        BlockAddress = owner.History.Store.Blocks[Block].Address;

        if (Batches?.GetPart(Block) is not { } part)
        {
            return;
        }

        PartType = part.Type;

        Title = part.Title;

        IsAvailable = true;

        IsVectorsAvailable = IsHeader;

        FieldMarkers = IsHeader ? GetFieldMarkers() : null;

        NotifyPartChanged();
    }

    public void Refresh(TracePosition position)
    {
        if (!IsAvailable || Batches is not { } batches || Owner is not { } owner)
        {
            return;
        }

        if (Current?.HasChanged(position) == false)
        {
            Loader.Cancel();

            return;
        }

        var block = Block;

        var blockAddress = BlockAddress;

        var type = PartType;

        var size = owner.History.Store.Blocks[Block].Size;

        Loader.Request(() => ReadSnapshot(batches, block, blockAddress, size, type, position));
    }

    public void OpenBatch()
    {
        if (Current is { State.Address: var address })
        {
            Owner?.RequestMemory(address);
        }
    }

    public void OpenNextPage()
    {
        if (NextPageAddress != 0)
        {
            Owner?.RequestMemory(NextPageAddress);
        }
    }

    public void OpenVector(int ordinal)
    {
        if (GetVector(ordinal) is { Buffer: not 0 } vector)
        {
            Owner?.RequestMemory(vector.Buffer);
        }
    }

    public int? GetVectorId(int ordinal, bool isVectorsTable)
        => isVectorsTable
            ? GetVector(ordinal)?.Id
            : ordinal == VectorValueColumn && Current is { VectorId: > 0 } current ? current.VectorId : null;

    public SqlDbType? GetVectorType(int vectorId)
        => Current is { State.Address: var batch } ? Owner?.History.GetVectorType(batch, vectorId) : null;

    public void SetVectorType(int vectorId, SqlDbType? type)
    {
        if (Current is not { State.Address: var batch } || Owner is not { } owner)
        {
            return;
        }

        owner.History.SetVectorType(batch, vectorId, type);

        Source = null;

        PartSource = null;

        Show();
    }

    public void Clear()
    {
        Owner = null;

        Batches = null;

        Block = -1;

        Loader.Cancel();

        Snapshot = null;

        IsAvailable = false;

        IsVectorsAvailable = false;

        Title = string.Empty;

        FieldMarkers = null;

        EmptyText = string.Empty;

        ClearContent();

        NotifyPartChanged();
    }

    partial void OnSelectedHeaderRowChanged(ResultRow<long>? value)
    {
        if (value is not { Id: var index } || index < 0 || index >= BatchReader.Fields.Count)
        {
            return;
        }

        var field = BatchReader.Fields[(int)index];

        Outline(field.Offset, field.Length, field.Name, FieldColours[(int)index]);
    }

    partial void OnSelectedPartRowChanged(ResultRow<long>? value)
    {
        if (value is not { Id: var id } || Current is not { State: var state } current)
        {
            return;
        }

        var row = (int)id;

        switch (PartType)
        {
            case BatchPartType.VectorArray when row < state.Vectors.Count:
                Outline(GetOffset(state.VectorArrayAddress) + row * BatchReader.VectorEntrySize,
                        BatchReader.VectorEntrySize,
                        $"Vector {state.Vectors[row].Id}");
                break;

            case BatchPartType.Selection:
                Outline(GetOffset(state.SelectionAddress) + row * SelectionEntrySize, SelectionEntrySize, $"Index {row}");
                break;

            case BatchPartType.Vector when current.VectorId > 0:
                Outline(GetOffset(state.Vectors[current.VectorId - 1].Buffer) + row * SlotSize, SlotSize, $"Row {row}");
                break;

            case BatchPartType.DeepData when PartSource is DeepDataRowSource source && row < source.Entries.Count:
                Outline(GetOffset(source.Entries[row].Address), source.Entries[row].Size, $"Entry {row}");
                break;
        }
    }

    private void NotifyPartChanged()
    {
        OnPropertyChanged(nameof(IsHeader));

        OnPropertyChanged(nameof(IsBatchLinkVisible));

        OnPropertyChanged(nameof(IsBitmap));

        OnPropertyChanged(nameof(IsVector));

        OnPropertyChanged(nameof(IsDeepData));

        OnPropertyChanged(nameof(IsVectorNameVisible));

        OnPropertyChanged(nameof(IsPartTableVisible));
    }

    private void ApplySnapshot(BatchSnapshot? snapshot)
    {
        Snapshot = snapshot;

        Show();
    }

    private static BatchSnapshot? ReadSnapshot(MemoryHistoryBatches batches,
                                               int block,
                                               ulong blockAddress,
                                               int size,
                                               BatchPartType type,
                                               TracePosition position)
    {
        if (batches.Read(block, position) is not { } batch)
        {
            return null;
        }

        var reader = batch.Reader;

        List<ulong?> fields = type == BatchPartType.Header ? [.. BatchReader.Fields.Select(f => ReadField(batch, f))] : [];

        var bitmap = type is BatchPartType.AllocationBitmap or BatchPartType.PureBitmap ? new byte[size] : [];

        var bitmapRead = bitmap.Length > 0 ? reader.Read(blockAddress, bitmap) : 0;

        var isDeepData = type == BatchPartType.DeepData;

        return new BatchSnapshot(batch,
                                 fields,
                                 bitmap,
                                 bitmapRead,
                                 isDeepData ? DeepDataReader.ReadEntries(reader, blockAddress, size) : [],
                                 isDeepData ? DeepDataReader.GetNextPage(reader, blockAddress) : 0,
                                 GetDeepValues(batch, type));
    }

    private static Dictionary<(int Vector, int Row), DeepDataEntry> GetDeepValues(MemoryHistoryBatch batch, BatchPartType type)
    {
        var values = new Dictionary<(int Vector, int Row), DeepDataEntry>();

        List<BatchVector> vectors = type switch
        {
            BatchPartType.Header => batch.State.Vectors,
            BatchPartType.Vector when batch.VectorId > 0 => [batch.State.Vectors[batch.VectorId - 1]],
            _ => []
        };

        foreach (var vector in vectors)
        {
            for (var row = 0; row < vector.Slots.Length; row++)
            {
                if (vector.IsDeepReference(row) && DeepDataReader.ReadValue(batch.Reader, vector.Slots[row]) is { } entry)
                {
                    values[(vector.Id, row)] = entry;
                }
            }
        }

        return values;
    }

    private void Show()
    {
        if (Snapshot is not { Batch: var current } snapshot)
        {
            ShowEmpty();

            return;
        }

        var state = current.State;

        EmptyText = string.Empty;

        Address = $"0x{state.Address:X16}";

        Vectors = $"{state.Vectors.Count:N0}";

        Capacity = $"{state.Capacity:N0} Rows";

        ActiveRows = $"{state.ActiveRowCount:N0}";

        switch (PartType)
        {
            case BatchPartType.Header:
                ShowHeader(snapshot);

                ShowVectors(state, current.VectorId);
                break;

            case BatchPartType.AllocationBitmap or BatchPartType.PureBitmap:
                ShowBitmap(snapshot);
                break;

            case BatchPartType.VectorArray:
                ShowVectorArray(state);
                break;

            case BatchPartType.Selection:
                ShowSelection(state);
                break;

            case BatchPartType.DeepData:
                ShowDeepData(snapshot);
                break;

            default:
                ShowVector(state, current.VectorId);
                break;
        }
    }

    private void ShowEmpty()
    {
        ClearContent();

        EmptyText = IsVector ? NoVector : NoBatch;
    }

    private void ClearContent()
    {
        Source = null;

        PartSource = null;

        HeaderTable = null;

        Table = null;

        PartTable = null;

        HeaderKey = string.Empty;

        Address = string.Empty;

        Vectors = string.Empty;

        Capacity = string.Empty;

        ActiveRows = string.Empty;

        VectorName = string.Empty;

        NextPage = string.Empty;

        NextPageAddress = 0;

        IsNextPageVisible = false;

        Bits = string.Empty;

        BitmapValue = string.Empty;
    }

    private void ShowHeader(BatchSnapshot snapshot)
    {
        var values = snapshot.Fields;

        var key = string.Join(",", values);

        if (key == HeaderKey)
        {
            return;
        }

        HeaderKey = key;

        var columns = new List<ResultColumn>
        {
            new(0, "Field", typeof(string), false) { Width = FieldWidth },
            new(1, "Offset", typeof(string), false) { Width = OffsetWidth, Alignment = ResultAlignment.Right },
            new(2, "Value", typeof(string), true) { Width = VectorWidth }
        };

        var rows = BatchReader.Fields.Select((f, i) => new ResultRow<long>([new SwatchText(f.Name, FieldColours[i]),
                                                                             $"0x{f.Offset:X}",
                                                                             GetFieldCell(f, values[i])])
        {
            Id = i
        });

        HeaderTable = new QueryResultSet { Columns = columns, Rows = [.. rows] };
    }

    private object? GetFieldCell(BatchField field, ulong? value)
    {
        if (value is not { } found)
        {
            return null;
        }

        if (field.IsHex)
        {
            return $"0x{found:X8}";
        }

        if (!field.IsPointer)
        {
            return $"{found:N0}";
        }

        return found != 0 && Owner is { } owner && owner.History.GetBlock(found, owner.Position) >= 0
            ? new ActionLinks([new ActionLink($"0x{found:X16}", () => owner.RequestMemory(found))])
            {
                Suffix = owner.History.GetPointerTarget(found, owner.Position)
            }
            : $"0x{found:X16}";
    }

    private static ulong? ReadField(MemoryHistoryBatch batch, BatchField field)
    {
        var address = batch.State.Address + (ulong)field.Offset;

        if (field.IsPointer)
        {
            return batch.Reader.TryRead(address, out ulong pointer) ? pointer : null;
        }

        return batch.Reader.TryRead(address, out uint number) ? number : null;
    }

    private void ShowVectors(BatchState state, int vectorId)
    {
        if (Source is { } source && Table is not null && source.IsSameLayout(state, vectorId))
        {
            source.SetState(state);

            Version++;

            return;
        }

        Source = new BatchRowSource(state, vectorId, GetSlotCell);

        Table = BuildVectorsTable(state, vectorId, Source);
    }

    private void ShowBitmap(BatchSnapshot snapshot)
    {
        var bytes = snapshot.Bitmap;

        var read = snapshot.BitmapRead;

        Bits = GetBits(bytes, Math.Min(snapshot.Batch.State.Vectors.Count, read * BitGroup));

        BitmapValue = read == bytes.Length ? $"0x{Convert.ToHexString([.. Enumerable.Reverse(bytes)])}" : string.Empty;
    }

    private static string GetBits(byte[] bytes, int count)
    {
        var bits = new StringBuilder();

        for (var index = 0; index < count; index++)
        {
            if (index > 0 && index % BitGroup == 0)
            {
                bits.Append(' ');
            }

            bits.Append((bytes[index / BitGroup] & (1 << (index % BitGroup))) != 0 ? '1' : '0');
        }

        return bits.ToString();
    }

    private void ShowVectorArray(BatchState state)
    {
        if (PartSource is VectorArrayRowSource source && PartTable is { } table && table.Rows.Count == state.Vectors.Count)
        {
            source.SetState(state);

            PartVersion++;

            return;
        }

        var arraySource = new VectorArrayRowSource(state, BlockAddress, a => Owner?.RequestMemory(a));

        PartSource = arraySource;

        PartTable = BuildTable(arraySource,
                               state.Vectors.Count,
                               new(0, "Vector", typeof(int), false) { Width = RowWidth, Alignment = ResultAlignment.Right },
                               new(1, "Offset", typeof(string), false) { Width = OffsetWidth, Alignment = ResultAlignment.Right },
                               new(2, "Buffer", typeof(string), true) { Width = VectorWidth },
                               new(3, "Current Page", typeof(string), true) { Width = VectorWidth },
                               new(4, "First Page", typeof(string), true) { Width = VectorWidth });
    }

    private void ShowSelection(BatchState state)
    {
        if (PartSource is SelectionRowSource source && PartTable is { } table && table.Rows.Count == state.Selection.Length)
        {
            source.SetState(state);

            PartVersion++;

            return;
        }

        var selectionSource = new SelectionRowSource(state, BlockAddress);

        PartSource = selectionSource;

        PartTable = BuildTable(selectionSource,
                               state.Selection.Length,
                               new(0, "Index", typeof(int), false) { Width = RowWidth, Alignment = ResultAlignment.Right },
                               new(1, "Offset", typeof(string), false) { Width = OffsetWidth, Alignment = ResultAlignment.Right },
                               new(2, "Row", typeof(int), false) { Width = RowWidth, Alignment = ResultAlignment.Right },
                               new(3, "Raw", typeof(string), false) { Width = OffsetWidth });
    }

    private void ShowVector(BatchState state, int vectorId)
    {
        if (vectorId <= 0 || vectorId > state.Vectors.Count)
        {
            ShowEmpty();

            return;
        }

        var vector = state.Vectors[vectorId - 1];

        VectorName = vector.IsPure ? $"Vector {vector.Id} ({PureBadge})" : $"Vector {vector.Id}";

        if (PartSource is VectorRowSource source && PartTable is not null && source.IsSameLayout(vector))
        {
            source.SetState(state, vector);

            PartVersion++;

            return;
        }

        var vectorSource = new VectorRowSource(state, vector, BlockAddress, GetSlotCell);

        var type = GetVectorType(vector.Id);

        PartSource = vectorSource;

        PartTable = BuildTable(vectorSource,
                               vector.Slots.Length,
                               new(0, "Row", typeof(int), false) { Width = RowWidth, Alignment = ResultAlignment.Right },
                               new(1, "Selection", typeof(int), true) { Width = SelectionWidth, Alignment = ResultAlignment.Right },
                               new(2, "Offset", typeof(string), false) { Width = OffsetWidth, Alignment = ResultAlignment.Right },
                               new(VectorValueColumn, "Value", typeof(string), true)
                               {
                                   Width = type is null ? VectorWidth : TypedVectorWidth,
                                   TypeName = type?.ToString()
                               });
    }

    private void ShowDeepData(BatchSnapshot snapshot)
    {
        var batch = snapshot.Batch;

        var entries = snapshot.Entries;

        VectorName = batch.VectorId > 0 ? $"Vector {batch.VectorId}" : NotAttached;

        NextPageAddress = snapshot.NextPage;

        NextPage = $"0x{NextPageAddress:X16}";

        IsNextPageVisible = NextPageAddress != 0;

        if (PartSource is DeepDataRowSource source && PartTable is { } table && table.Rows.Count == entries.Count)
        {
            source.SetEntries(entries);

            PartVersion++;

            return;
        }

        var deepSource = new DeepDataRowSource(entries,
                                               BlockAddress,
                                               () => Current is { VectorId: > 0 } c ? GetVectorType(c.VectorId) : null);

        var type = batch.VectorId > 0 ? GetVectorType(batch.VectorId) : null;

        PartSource = deepSource;

        PartTable = BuildTable(deepSource,
                               entries.Count,
                               new(0, "Entry", typeof(int), false) { Width = RowWidth, Alignment = ResultAlignment.Right },
                               new(1, "Offset", typeof(string), false) { Width = OffsetWidth, Alignment = ResultAlignment.Right },
                               new(2, "Length", typeof(int), false) { Width = RowWidth, Alignment = ResultAlignment.Right },
                               new(VectorValueColumn, "Value", typeof(string), true)
                               {
                                   Width = type is null ? TypedVectorWidth : VectorWidth,
                                   TypeName = type?.ToString()
                               });
    }

    private object? GetSlotCell(BatchVector vector, int row)
    {
        var slot = vector.Slots[row];

        var type = GetVectorType(vector.Id);

        if (Snapshot?.DeepValues.GetValueOrDefault((vector.Id, row)) is { } entry)
        {
            var text = type is null ? BatchSlotFormatter.Format(slot, null) : BatchSlotFormatter.FormatDeep(entry.Data, type);

            return new ActionLinks([new ActionLink(text, () => Owner?.RequestMemory(entry.Address, entry.Size))]);
        }

        return BatchSlotFormatter.Format(slot, type);
    }

    private BatchVector? GetVector(int ordinal)
        => Current is { State.Vectors: var vectors } && ordinal >= BatchRowSource.FixedColumnCount
                                                      && ordinal - BatchRowSource.FixedColumnCount < vectors.Count
            ? vectors[ordinal - BatchRowSource.FixedColumnCount]
            : null;

    private int GetOffset(ulong address) => (int)(address - BlockAddress);

    private void Outline(int offset, int length, string description, Colour? colour = null)
    {
        if (Owner is { } owner)
        {
            owner.SelectedChangeSpan = new LogRecordAnnotation
            {
                Offset = offset,
                Length = length,
                Description = description,
                OutlineColour = colour?.ToWindowsColor()
            };
        }
    }

    private QueryResultSet BuildVectorsTable(BatchState state, int vectorId, BatchRowSource source)
    {
        var columns = new List<ResultColumn>
        {
            new(0, "Row", typeof(int), false) { Width = RowWidth, Alignment = ResultAlignment.Right },
            new(1, "Selection", typeof(int), true) { Width = SelectionWidth, Alignment = ResultAlignment.Right }
        };

        foreach (var vector in state.Vectors)
        {
            var type = GetVectorType(vector.Id);

            columns.Add(new ResultColumn(BatchRowSource.FixedColumnCount + vector.Id - 1, $"Vector {vector.Id}", typeof(string), true)
            {
                Width = type is null ? VectorWidth : TypedVectorWidth,
                TypeName = type?.ToString(),
                Badge = vector.IsPure ? PureBadge : null,
                BadgeColour = vector.IsPure ? PureColour : null,
                IsHeaderLink = vector.Buffer != 0,
                BackgroundColour = vector.Id == vectorId ? OpenedVectorColour : null
            });
        }

        return BuildTable(source, state.RowCount, [.. columns]);
    }

    private static QueryResultSet BuildTable(IResultRowSource source, int rowCount, params List<ResultColumn> columns)
        => new()
        {
            Columns = columns,
            Rows = [.. Enumerable.Range(0, rowCount).Select(r => new ResultRow<long>(source, r) { Id = r })],
            Source = source
        };

    private static ObservableCollection<Marker> GetFieldMarkers()
        => new(BatchReader.Fields.Select((f, i) => new Marker
        {
            Name = f.Name,
            StartPosition = f.Offset,
            EndPosition = f.Offset + f.Length - 1,
            BackColour = FieldColours[i].ToWindowsColor(),
            MapColour = FieldColours[i].ToWindowsColor(),
            ForeColour = Colors.Black
        }));

    private sealed record BatchSnapshot(MemoryHistoryBatch Batch,
                                        List<ulong?> Fields,
                                        byte[] Bitmap,
                                        int BitmapRead,
                                        List<DeepDataEntry> Entries,
                                        ulong NextPage,
                                        Dictionary<(int Vector, int Row), DeepDataEntry> DeepValues);
}
