using System.Data;
using System.Runtime.InteropServices;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Iterators;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.RowMode.Windowing;

/// <summary>
/// Segment Operator
/// </summary>
/// <remarks>
/// Segment is a pass through operator that includes an additional field that contains 1 or 0 that flags if the segment grouping key value
/// has changed. Sequence Project uses this flag to determine partition boundaries.
///
/// The name of the additional field is defined by SegmentColumn and is a property of the operator.
///
/// Segment changes are detected by comparing the current key value to the previous key value. The operator relies on a sorted input for
/// this.
/// </remarks>
public sealed class SegmentIterator(IIteratorFactory factory) : IteratorBase, IUnaryIterator
{
    public override PageAddress? CurrentPageAddress => Input?.CurrentPageAddress;

    public override AccessStrategy? Strategy => Input?.Strategy;

    public IIterator? Input { get; private set; }

    public long RowCount { get; private set; }

    public long SegmentCount { get; private set; }

    public IReadOnlyList<string> GroupBy { get; private set; } = [];

    public string SegmentColumn { get; private set; } = string.Empty;

    /// <summary>
    /// The key of the segment already open, which the next row is compared against, one value per grouping column
    /// </summary>
    public IReadOnlyList<string> CurrentKeyValues { get; private set; } = [];

    /// <summary>
    /// The key of the row just read, one value per grouping column
    /// </summary>
    public IReadOnlyList<string> RowKeyValues { get; private set; } = [];

    public string RowKey { get; private set; } = string.Empty;

    private AccessKey SegmentKey { get; set; }

    private bool HasSegment { get; set; }

    public override async Task OpenAsync(IteratorDefinition definition,
                                         IteratorContext context,
                                         CancellationToken cancellationToken)
    {
        var segment = definition.Expect<SegmentDefinition>();

        if (string.IsNullOrEmpty(segment.SegmentColumn))
        {
            throw new ArgumentException("A segment needs the column its flag is written to");
        }

        if (Input is not null)
        {
            await CloseAsync();
        }

        await PrepareAsync(definition, context, cancellationToken);

        GroupBy = segment.GroupBy;
        SegmentColumn = segment.SegmentColumn;

        RowCount = 0;
        SegmentCount = 0;

        SegmentKey = AccessKey.Unbounded;
        HasSegment = false;

        CurrentKeyValues = [];
        RowKeyValues = [];

        RowKey = string.Empty;

        Input = factory.Create(segment.Source);

        await Input.OpenAsync(segment.Source, context, cancellationToken);
    }

    public override async Task<IRecord?> GetRowAsync(CancellationToken cancellationToken)
    {
        if (IsComplete || Input is null)
        {
            return null;
        }

        var row = await Input.GetRowAsync(cancellationToken);

        if (row is null)
        {
            CurrentRow = null;

            await EmitAsync(new AccessStep.Stopped(Input.StopReason ?? AccessPaths.Results.StopReason.PageExhausted),
                            cancellationToken);

            return null;
        }

        RowCount++;

        var key = GetKey(row);

        RowKey = KeyText(key);
        RowKeyValues = KeyValues(key);

        var isNewSegment = !HasSegment || (GroupBy.Count > 0 && !key.Equals(SegmentKey));

        if (isNewSegment)
        {
            SegmentCount++;
        }

        var flag = new ComputedField(SegmentColumn, AccessValue.FromInteger(SqlDbType.Bit, isNewSegment ? 1 : 0));

        var record = ComputedRecord.Extend(row, [flag]);

        var step = new AccessStep.SegmentRow(RowCount, isNewSegment)
        {
            EmittedRecord = record,
            SegmentCount = SegmentCount,
            Key = RowKey
        };

        await EmitAsync(step, cancellationToken);

        AdoptSegment(key, isNewSegment);

        CurrentRow = ProjectedRecord.Project(record, OutputList);

        return CurrentRow;
    }

    public override async Task CloseAsync()
    {
        if (Input is not null)
        {
            await Input.CloseAsync();
        }

        SegmentKey = AccessKey.Unbounded;
        HasSegment = false;

        CurrentKeyValues = [];
        RowKeyValues = [];

        RowKey = string.Empty;

        await base.CloseAsync();
    }

    /// <summary>
    /// Takes the row's key as the open segment's, once the step carrying the comparison has been delivered
    /// </summary>
    /// <remarks>
    /// A trace parks inside the step, so taking the key any earlier would leave the two reading equal on exactly the rows where they
    /// differed, which are the only rows worth watching.
    /// </remarks>
    private void AdoptSegment(AccessKey key, bool isNewSegment)
    {
        HasSegment = true;

        if (!isNewSegment)
        {
            return;
        }

        SegmentKey = key;
        CurrentKeyValues = RowKeyValues;
    }

    private AccessKey GetKey(IRecord record)
    {
        if (GroupBy.Count == 0)
        {
            return AccessKey.Unbounded;
        }

        var source = new RecordRowValueSource(record);

        var values = new AccessValue[GroupBy.Count];

        for (var index = 0; index < GroupBy.Count; index++)
        {
            var column = GroupBy[index];

            if (FindField(record, column) is null)
            {
                throw new InvalidOperationException($"Row has no column '{column}' to segment on");
            }

            values[index] = source.GetValue(-1, column).WithColumnName(column);
        }

        return new AccessKey(ImmutableCollectionsMarshal.AsImmutableArray(values));
    }

    private static IReadOnlyList<string> KeyValues(AccessKey key)
        => key.IsUnbounded ? [] : [.. key.Values.Select(AccessValueFormatter.ToText)];

    private static string KeyText(AccessKey key)
        => key.IsUnbounded ? string.Empty : string.Join(", ", key.Values.Select(AccessValueFormatter.ToText));

    private static RecordField? FindField(IRecord record, string column)
        => record.Fields.FirstOrDefault(f => string.Equals(f.ColumnStructure.ColumnName, column, StringComparison.OrdinalIgnoreCase));
}
