using InternalsViewer.Execution.AccessPaths.Aggregation;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Interfaces.AccessPaths.Binding;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Records;

public sealed class AggregateGroupRecord : IRecord
{
    private readonly List<RecordField> _groupFields;

    private readonly AggregateAccumulator[] _accumulators;

    public AggregateGroupRecord(IReadOnlyList<RecordField> groupFields, IReadOnlyList<AggregateColumn> columns)
    {
        _groupFields = [.. groupFields];

        _accumulators = [.. columns.Select(c => new AggregateAccumulator(c))];

        Fields = [.. _groupFields];

        Refresh();
    }

    public IReadOnlyList<AggregateAccumulator> Accumulators => _accumulators;

    public IReadOnlyList<AggregateValue> Values => [.. _accumulators.Select(a => a.Value)];

    public long RowCount { get; private set; }

    public int Slot => -1;

    public ushort Offset => 0;

    public List<RecordField> Fields { get; }

    public short ColumnCount => (short)Fields.Count;

    public bool IsGhost => false;

    public List<DataStructureItem> MarkItems { get; } = [];

    public void Add(IRowValueSource source, EvaluationContext context)
    {
        foreach (var accumulator in _accumulators)
        {
            var value = accumulator.Column.Argument is { } argument
                ? PredicateEvaluator.Resolve(argument, source, context)
                : AccessValue.Null;

            accumulator.Add(value);
        }

        RowCount++;

        Refresh();
    }

    public void Combine(AggregateGroupRecord other)
    {
        for (var index = 0; index < _accumulators.Length; index++)
        {
            _accumulators[index].Combine(other._accumulators[index]);
        }

        RowCount += other.RowCount;

        Refresh();
    }

    private void Refresh()
    {
        Fields.RemoveRange(_groupFields.Count, Fields.Count - _groupFields.Count);

        foreach (var accumulator in _accumulators)
        {
            Fields.Add(new ComputedField(accumulator.Column.Column, accumulator.Result));
        }
    }
}
