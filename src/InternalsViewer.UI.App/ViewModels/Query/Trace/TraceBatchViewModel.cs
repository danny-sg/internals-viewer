using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.BatchMode.Normalization;
using InternalsViewer.Execution.BatchMode.Vectors;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Batch;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

/// <summary>
/// The batch the trace is parked on, shown as its vectors, its selection and its deep data
/// </summary>
public sealed partial class TraceBatchViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _hasBatch;

    [ObservableProperty]
    private bool _isSpent;

    [ObservableProperty]
    private IReadOnlyList<BatchColumnView> _columns = [];

    [ObservableProperty]
    private IReadOnlyList<BatchRowView> _rows = [];

    [ObservableProperty]
    private IReadOnlyList<BatchSelectionRow> _selection = [];

    [ObservableProperty]
    private IReadOnlyList<BatchDeepDataRow> _deepData = [];

    [ObservableProperty]
    private BatchDeepDataRow? _selectedDeepData;

    [ObservableProperty]
    private IReadOnlyList<BatchDetailItem> _deepDataDetail = [];

    [ObservableProperty]
    private string _deepDataSummary = string.Empty;

    [ObservableProperty]
    private string _selectionSummary = string.Empty;

    [ObservableProperty]
    private string _purity = string.Empty;

    [ObservableProperty]
    private bool _isPureVector;

    [ObservableProperty]
    private int _rowGroupId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BatchName))]
    private long _batchNumber;

    [ObservableProperty]
    private BatchValueSelection? _selectedSlot;

    [ObservableProperty]
    private IReadOnlyList<BatchDetailItem> _detail = [];

    [ObservableProperty]
    private string _detailHeading = string.Empty;

    /// <summary>
    /// Raised when a deep data link asks for the value it points at
    /// </summary>
    public event Action<int>? DeepDataRequested;

    /// <summary>
    /// The operator that creates this batch
    /// </summary>
    public int NodeId { get; init; }

    /// <summary>
    /// The operator name the tab is titled with
    /// </summary>
    public string Title { get; init; } = "Batch";

    /// <summary>
    /// Whether the columns changed shape, which is when a grid has to rebuild rather than repaint
    /// </summary>
    public int ColumnVersion { get; private set; }

    public string BatchName => $"Batch {BatchNumber}";

    public void Update(ExecutionBatch? batch, long number, IReadOnlyList<BatchVector> scope)
    {
        if (batch is null)
        {
            Clear();

            return;
        }

        UpdateColumns(batch.Vectors, scope);

        UpdateRows(batch, batch.Vectors);

        UpdateDeepData(batch);

        RowGroupId = batch.RowGroupId;

        BatchNumber = number;

        IsPureVector = batch.IsPureVector;

        Purity = batch.IsPureVector ? "Pure" : "Impure";

        SelectionSummary = $"{batch.SelectionVector.RowCount}/{batch.RowCount}";

        HasBatch = true;

        IsSpent = false;

        RefreshDetail();
    }

    public void MarkSpent() => IsSpent = HasBatch;

    public void Clear()
    {
        HasBatch = false;

        IsSpent = false;

        Columns = [];

        Rows = [];

        Selection = [];

        DeepData = [];

        DeepDataDetail = [];

        DeepDataSummary = string.Empty;

        SelectedDeepData = null;

        Detail = [];

        DetailHeading = string.Empty;

        SelectionSummary = string.Empty;

        BatchNumber = 0;

        Purity = string.Empty;

        SelectedSlot = null;

        ColumnVersion++;
    }

    public void SelectSlot(BatchValueSelection? selection)
    {
        SelectedSlot = selection;

        RefreshDetail();
    }

    public void RequestDeepData(int index) => DeepDataRequested?.Invoke(index);

    private void UpdateColumns(IReadOnlyList<BatchVector> vectors, IReadOnlyList<BatchVector> scope)
    {
        var unchanged = Columns.Count == vectors.Count
                        && Columns.Select((c, i) => c.Name == vectors[i].Column.Name
                                                    && ReferenceEquals(c.Source, vectors[i].Source))
                                  .All(m => m);

        if (unchanged)
        {
            ApplyScope(vectors, scope);

            return;
        }

        Columns = [.. vectors.Select((v, i) => new BatchColumnView
        {
            Ordinal = i,
            Name = string.IsNullOrEmpty(v.Column.Name) ? $"Column {i}" : v.Column.Name,
            Column = v.Column,
            Source = v.Source
        })];

        ApplyScope(vectors, scope);

        ColumnVersion++;

        SelectedSlot = null;
    }

    private void ApplyScope(IReadOnlyList<BatchVector> vectors, IReadOnlyList<BatchVector> scope)
    {
        for (var i = 0; i < Columns.Count && i < vectors.Count; i++)
        {
            Columns[i].IsInScope = scope.Contains(vectors[i]);
        }
    }

    private void UpdateRows(ExecutionBatch batch, IReadOnlyList<BatchVector> vectors)
    {
        var rows = new BatchRowView[batch.RowCount];

        var selected = new bool[batch.RowCount];

        var selection = new BatchSelectionRow[batch.SelectionVector.RowCount];

        for (var i = 0; i < batch.SelectionVector.RowCount; i++)
        {
            var row = batch.SelectionVector[i];

            selection[i] = new BatchSelectionRow { Index = row };

            if (row < selected.Length)
            {
                selected[row] = true;
            }
        }

        for (var row = 0; row < rows.Length; row++)
        {
            var slots = new BatchValue[vectors.Count];

            for (var ordinal = 0; ordinal < vectors.Count; ordinal++)
            {
                var vector = vectors[ordinal];

                slots[ordinal] = row < vector.Values.Length ? vector[row] : default;
            }

            rows[row] = new BatchRowView
            {
                RowIndex = row,
                IsSelected = selected[row],
                Values = slots
            };
        }

        Rows = rows;

        Selection = selection;
    }

    private void UpdateDeepData(ExecutionBatch batch)
    {
        var context = batch.DeepDataContext;

        var rows = new BatchDeepDataRow[context.Count];

        for (var index = 0; index < rows.Length; index++)
        {
            var address = context.AddressOf(index);

            var data = context.Get(address);

            rows[index] = new BatchDeepDataRow
            {
                Index = index,
                Address = address,
                Length = data.Length,
                Data = Convert.ToHexString(data)
            };
        }

        DeepData = rows;

        DeepDataSummary = rows.Length == 0
            ? "No Entries"
            : $"{rows.Length:N0} {(rows.Length == 1 ? "Entry" : "Entries")}, {SizeFormat.Format(rows.Sum(r => (long)r.Length))}";

        SelectedDeepData = SelectedDeepData is { } selected && selected.Index < rows.Length
            ? rows[selected.Index]
            : null;
    }

    public void SelectDeepData(BatchDeepDataRow? row)
    {
        SelectedDeepData = row;
    }

    partial void OnSelectedDeepDataChanged(BatchDeepDataRow? value)
        => DeepDataDetail = value is null ? [] : BatchValueDescriber.DescribeDeepData(value);

    private void RefreshDetail()
    {
        if (SelectedSlot is not { } selection
            || selection.Ordinal >= Columns.Count
            || selection.RowIndex >= Rows.Count)
        {
            Detail = [];

            DetailHeading = string.Empty;

            return;
        }

        var column = Columns[selection.Ordinal];

        var row = Rows[selection.RowIndex];

        DetailHeading = $"{column.Name} [{selection.RowIndex}]";

        Detail = BatchValueDescriber.Describe(column, row, DeepData);
    }
}
