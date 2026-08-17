using System.Collections.Immutable;
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

namespace InternalsViewer.Execution.Iterators.Windowing;

/// <summary>
/// Segment operator
/// </summary>
/// <remarks>
/// Every row is passed straight on carrying one extra column, set when the row is the first of a group. Only the previous row's key is
/// held, so the input has to already be ordered on the grouping columns for the groups to come out whole. An empty grouping list makes
/// the whole input one segment, which is what an OVER clause with no PARTITION BY produces.
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

    private AccessKey CurrentKey { get; set; }

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

        CurrentKey = AccessKey.Unbounded;
        HasSegment = false;

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

        var isNewSegment = !HasSegment || (GroupBy.Count > 0 && !key.Equals(CurrentKey));

        if (isNewSegment)
        {
            CurrentKey = key;
            HasSegment = true;

            SegmentCount++;
        }

        var flag = new ComputedField(SegmentColumn, AccessValue.FromInteger(SqlDbType.Bit, isNewSegment ? 1 : 0));

        var record = ComputedRecord.Extend(row, [flag]);

        var step = new AccessStep.SegmentRow(RowCount, isNewSegment)
        {
            EmittedRecord = record,
            SegmentCount = SegmentCount,
            Key = KeyText(CurrentKey),
            Column = SegmentColumn
        };

        await EmitAsync(step, cancellationToken);

        CurrentRow = ProjectedRecord.Project(record, OutputList);

        return CurrentRow;
    }

    public override async Task CloseAsync()
    {
        if (Input is not null)
        {
            await Input.CloseAsync();
        }

        await base.CloseAsync();
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

    private static string KeyText(AccessKey key)
        => key.IsUnbounded ? string.Empty : string.Join(", ", key.Values.Select(AccessValueFormatter.ToText));

    private static RecordField? FindField(IRecord record, string column)
        => record.Fields.FirstOrDefault(f => string.Equals(f.ColumnStructure.ColumnName, column, StringComparison.OrdinalIgnoreCase));
}
