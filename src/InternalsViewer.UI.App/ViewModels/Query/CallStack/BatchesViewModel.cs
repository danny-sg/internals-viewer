using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.BatchMode.Normalization;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Batches;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Query.CallStack;
using InternalsViewer.UI.App.Models.Query.Simulation.Batch;

namespace InternalsViewer.UI.App.ViewModels.Query.CallStack;

public sealed partial class BatchesViewModel(CallStackNode node, CallStoreLookup lookup, CallTreeTabViewModel owner, bool isDecoded)
    : ObservableObject, IDisposable
{
    private const int CallWidth = 70;

    private const int ThreadWidth = 80;

    private const int CountWidth = 80;

    private const int SelectedWidth = 110;

    private const int SizeWidth = 90;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private QueryResultSet? _table;

    [ObservableProperty]
    private ResultRow<long>? _selectedRow;

    [ObservableProperty]
    private int? _selectedCall;

    [ObservableProperty]
    private int _tableVersion;

    [ObservableProperty]
    private IReadOnlyList<BatchDetailItem> _detail = [];

    [ObservableProperty]
    private string _detailHeading = string.Empty;

    [ObservableProperty]
    private bool _hasDetail;

    public CallStackNode Node { get; } = node;

    public string Title => $"{Node.Symbol} Batches";

    public CallTreeTabViewModel Owner { get; } = owner;

    private CancellationTokenSource Cancellation { get; } = new();

    private bool IsDecoded { get; set; } = isDecoded;

    private (BatchSlotRow Row, int Ordinal)? SelectedSlot { get; set; }

    public bool IsShowing(CallStackNode other)
        => Node.Frame is { } frame
           && other.Frame is { } otherFrame
           && frame.Address == otherFrame.Address
           && frame.Instance == otherFrame.Instance;

    public async Task LoadAsync()
    {
        if (Node.Frame is not { } frame)
        {
            return;
        }

        IsLoading = true;

        var token = Cancellation.Token;

        try
        {
            var table = await Task.Run(() => BuildTable(frame.Address, frame.Instance), token);

            Table = table;

            Message = table.Rows.Count == 0 ? "No Batches" : null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void SetDecoded(bool decoded)
    {
        if (IsDecoded == decoded)
        {
            return;
        }

        IsDecoded = decoded;

        TableVersion++;

        RefreshDetail();
    }

    public BatchLineage? LoadBatch(int call)
        => Node.Frame is { } frame ? lookup.Batches.GetLineage(frame.Address, frame.Instance, call) : null;

    public static IReadOnlyList<BatchSlotRow> GetVectorRows(BatchLineage lineage)
        => [.. Enumerable.Range(0, lineage.RowCount).Select(r => new BatchSlotRow(lineage, r))];

    public static IReadOnlyList<BatchSelectionRow> GetSelectionRows(BatchCapture capture)
        => [.. capture.Selection.Select(r => new BatchSelectionRow { Index = r })];

    public static IReadOnlyList<BatchDeepDataEntry> GetDeepDataRows(BatchCapture capture)
        => [.. capture.Vectors.SelectMany(v => v.DeepValues
                                                .OrderBy(d => d.Key)
                                                .Select(d => new BatchDeepDataEntry
                                                {
                                                    VectorId = v.VectorId,
                                                    RowIndex = d.Key,
                                                    Address = (v.GetSlot(d.Key) ?? 0) & ~1ul,
                                                    Length = d.Value.Length,
                                                    Data = Convert.ToHexString(d.Value.Data),
                                                    Text = GetText(d.Value.Data)
                                                }))];

    public string GetColumnName(BatchCapture capture, int ordinal)
        => lookup.BatchDecoder?.GetColumnName(capture.Iterator, ordinal) ?? $"Column {ordinal}";

    public string GetSlotText(BatchSlotRow row, int ordinal)
    {
        if (row.Lineage.GetSlot(ordinal, row.RowIndex) is not { } found)
        {
            return string.Empty;
        }

        if (IsDecoded
            && lookup.BatchDecoder?.Decode(row.Capture.Iterator, ordinal, found.Slot, found.Vector.GetDeepValue(row.RowIndex)) is { } text)
        {
            return text;
        }

        return $"0x{found.Slot:X16}";
    }

    public static bool IsPure(BatchSlotRow row, int ordinal) => ordinal < row.Capture.Vectors.Count && row.Capture.Vectors[ordinal].IsPure;

    public void SelectSlot(BatchSlotRow row, int ordinal)
    {
        SelectedSlot = (row, ordinal);

        RefreshDetail();
    }

    public void ClearSlot()
    {
        SelectedSlot = null;

        RefreshDetail();
    }

    public CallStackNode? GetCallNode(int call)
        => Node.Frame is { } frame
           && lookup.Store.GetCallList(frame.Address, frame.Instance) is { } calls
           && call >= 0
           && call < calls.Count
               ? lookup.Store.GetNode(calls[call])
               : null;

    public void Dispose()
    {
        Cancellation.Cancel();

        Cancellation.Dispose();
    }

    partial void OnSelectedRowChanged(ResultRow<long>? value)
    {
        ClearSlot();

        SelectedCall = value is { Id: var call } ? (int)call : null;

        if (SelectedCall is { } selected && GetCallNode(selected) is { } callNode)
        {
            Owner.Tree.ShowCall(callNode, selected);
        }
    }

    private static string GetText(byte[] data) => string.Concat(data.Select(b => b is >= 32 and < 127 ? (char)b : '.'));

    private QueryResultSet BuildTable(ulong address, ulong instance)
    {
        var summaries = lookup.Batches.GetSummaries(address, instance);

        var callList = lookup.Store.GetCallList(address, instance);

        var columns = new List<ResultColumn>
        {
            new(0, "Call", typeof(long), false) { Width = CallWidth, Alignment = ResultAlignment.Right },
            new(1, "Thread", typeof(uint), false) { Width = ThreadWidth, Alignment = ResultAlignment.Right },
            new(2, "Batch Size", typeof(int), false) { Width = SizeWidth, Alignment = ResultAlignment.Right },
            new(3, "Vectors", typeof(int), false) { Width = CountWidth, Alignment = ResultAlignment.Right },
            new(4, "Selected", typeof(string), false) { Width = SelectedWidth, Alignment = ResultAlignment.Right }
        };

        var rows = new ResultRow<long>[summaries.Count];

        for (var index = 0; index < rows.Length; index++)
        {
            var summary = summaries[index];

            var call = callList is not null && summary.Call < callList.Count ? callList[summary.Call] : null;

            object? callValue = call is not null && lookup.Store.GetNode(call) is { } callNode
                ? new CallValue(callNode, summary.Call)
                : (long)summary.Call + 1;

            var selected = summary.InputSelectedCount is { } input
                ? $"{summary.SelectedCount:N0}/{input:N0}"
                : summary.SelectedCount.ToString("N0", CultureInfo.InvariantCulture);

            int? capacity = summary.Capacity > 0 ? summary.Capacity : null;

            rows[index] = new ResultRow<long>([callValue, call?.ThreadId, capacity, summary.VectorCount, selected])
            {
                Id = summary.Call
            };
        }

        return new QueryResultSet { Columns = columns, Rows = rows };
    }

    private void RefreshDetail()
    {
        if (SelectedSlot is not { } selected)
        {
            Detail = [];

            DetailHeading = string.Empty;

            HasDetail = false;

            return;
        }

        DetailHeading = $"{GetColumnName(selected.Row.Capture, selected.Ordinal)} [{selected.Row.RowIndex}]";

        Detail = Describe(selected.Row, selected.Ordinal);

        HasDetail = true;
    }

    private IReadOnlyList<BatchDetailItem> Describe(BatchSlotRow row, int ordinal)
    {
        var items = new List<BatchDetailItem>
        {
            Item("Call", row.CallNumber.ToString(CultureInfo.InvariantCulture)),
            Item("Index", row.RowIndex.ToString(CultureInfo.InvariantCulture)),
            Item("Selected", row.IsSelected ? "Yes" : row.IsRemoved ? "No, Removed By This Operator" : "No")
        };

        if (ordinal >= row.Capture.Vectors.Count)
        {
            return items;
        }

        var vector = row.Capture.Vectors[ordinal];

        items.Add(Item("Vector Id", vector.VectorId.ToString(CultureInfo.InvariantCulture)));

        items.Add(Item("Pure", vector.IsPure ? "Yes" : "No"));

        if (row.Lineage.GetSlot(ordinal, row.RowIndex) is not { } found)
        {
            items.Add(Item("Slot", "Unreadable"));

            return items;
        }

        var slot = found.Slot;

        if (!ReferenceEquals(found.Vector, vector))
        {
            items.Add(Item("Read From", "The Operator's Input"));
        }

        var value = new BatchValue((long)slot);

        items.Add(Hex("Slot", slot));

        items.Add(Item("Slot Value", value.Value.ToString(CultureInfo.InvariantCulture)));

        var structure = lookup.BatchDecoder?.GetColumn(row.Capture.Iterator, ordinal);

        var valueType = GetValueType(value, structure);

        items.Add(Item("Slot Type", valueType is { } type ? StringExtensions.SplitString(type.ToString()) : "Unknown"));

        if (structure is not null)
        {
            items.Add(Item("Data Type", structure.DataType.ToString()));

            items.Add(Item("Domain", BatchValueNormalizer.GetDomain(structure.DataType).ToString()));
        }

        var deepValue = found.Vector.GetDeepValue(row.RowIndex);

        switch (valueType)
        {
            case BatchValueType.DictionaryReference:
                items.Add(Item("Dictionary", BatchValueDenormalizer.GetDictionaryNumber(value).ToString(CultureInfo.InvariantCulture)));

                items.Add(Item("Entry", BatchValueDenormalizer.GetDictionaryEntry(value).ToString(CultureInfo.InvariantCulture)));

                break;

            case BatchValueType.DeepDataReference:
                AddDeepValue(items, deepValue);

                break;
        }

        if (structure is not null && BatchDecoder.DecodeSlot(slot, deepValue, structure) is { } decoded)
        {
            items.Add(Item("Value", decoded));
        }

        return items;
    }

    private static BatchValueType? GetValueType(BatchValue value, ColumnStructure? structure)
    {
        if (structure is not null)
        {
            return BatchValueDenormalizer.GetValueType(value, BatchValueNormalizer.GetDomain(structure.DataType));
        }

        if (value.IsNull)
        {
            return BatchValueType.Null;
        }

        return value.IsDeepDataReference ? BatchValueType.DeepDataReference : null;
    }

    private static void AddDeepValue(List<BatchDetailItem> items, BatchDeepValue? deepValue)
    {
        if (deepValue is not { IsReadable: true })
        {
            items.Add(Item("Deep Data", "Unreadable"));

            return;
        }

        items.Add(Item("Length", deepValue.Length.ToString(CultureInfo.InvariantCulture)));

        items.Add(new BatchDetailItem { Name = "Data", Value = Convert.ToHexString(deepValue.Data), IsMonospaced = true });

        items.Add(Item("Text", GetText(deepValue.Data)));

        if (deepValue.IsTruncated)
        {
            items.Add(Item("Captured", $"{deepValue.Data.Length.ToString(CultureInfo.InvariantCulture)} Bytes"));
        }
    }

    private static BatchDetailItem Item(string name, string value) => new() { Name = name, Value = value };

    private static BatchDetailItem Hex(string name, ulong value) => new() { Name = name, Value = $"0x{value:X16}", IsMonospaced = true };
}
