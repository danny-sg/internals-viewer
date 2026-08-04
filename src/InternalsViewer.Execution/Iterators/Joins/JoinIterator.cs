using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Interfaces.Iterators.Joins;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.Joins;

public abstract class JoinIterator : IteratorBase, IJoinIterator
{
    public JoinInput Outer { get; protected set; } = null!;

    public JoinInput Inner { get; protected set; } = null!;

    public JoinType JoinType { get; protected set; } = JoinType.Inner;

    public int PairCount { get; protected set; }

    public override AccessStrategy? Strategy => Outer?.Iterator.Strategy;

    IJoinInput IJoinIterator.Outer => Outer;

    IJoinInput IJoinIterator.Inner => Inner;

    protected CancellationToken CurrentToken { get; private set; }

    protected bool IsOpen => Rows is not null;

    private IAsyncEnumerator<IRecord>? Rows { get; set; }

    public override async Task<IRecord?> GetRowAsync(CancellationToken cancellationToken)
    {
        if (IsComplete || Rows is null)
        {
            return null;
        }

        CurrentToken = cancellationToken;

        if (!await Rows.MoveNextAsync())
        {
            IsComplete = true;
            CurrentRow = null;

            return null;
        }

        CurrentRow = Rows.Current;

        return CurrentRow;
    }

    public override async Task CloseAsync()
    {
        if (Rows is not null)
        {
            await Rows.DisposeAsync();

            Rows = null;
        }

        if (Outer is not null)
        {
            await Outer.Iterator.CloseAsync();
        }

        if (Inner is not null)
        {
            await Inner.Iterator.CloseAsync();
        }

        await base.CloseAsync();
    }

    protected abstract IAsyncEnumerable<IRecord> RunAsync();

    protected void StartRows()
    {
        Rows = RunAsync().GetAsyncEnumerator(CancellationToken.None);
    }

    protected void ResetJoin(JoinType joinType)
    {
        JoinType = joinType;
        PairCount = 0;
    }

    protected IRecord MakeRow(IRecord? outer, IRecord? inner)
    {
        var combined = JoinedRecord.Combine(outer, inner)
                       ?? throw new InvalidOperationException("A join pair needs at least one side to make a row from");

        return ProjectedRecord.Project(combined, OutputList);
    }

    protected static AccessKey GetKey(IRecord record, IReadOnlyList<string> columns, string purpose)
    {
        var source = new RecordRowValueSource(record);

        var values = new AccessValue[columns.Count];

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];

            if (!record.Fields.Any(f => string.Equals(f.ColumnStructure.ColumnName, column, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Row has no column '{column}' to build the {purpose}");
            }

            values[index] = source.GetValue(-1, column).WithColumnName(column);
        }

        return new AccessKey([.. values]);
    }

    protected static bool HasNull(AccessKey key)
    {
        for (var index = 0; index < key.Count; index++)
        {
            if (key[index].IsNull)
            {
                return true;
            }
        }

        return false;
    }
}
