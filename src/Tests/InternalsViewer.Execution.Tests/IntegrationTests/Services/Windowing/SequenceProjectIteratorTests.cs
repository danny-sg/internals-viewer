using System.Data;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.AccessPaths.Windowing;
using InternalsViewer.Execution.Iterators.Stepping;
using InternalsViewer.Execution.Iterators.Windowing;
using InternalsViewer.Execution.Tests.Helpers;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Windowing;

public class SequenceProjectIteratorTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Row_Number_Counts_Every_Row_When_The_Input_Is_One_Partition()
    {
        var context = await LoadAsync();

        var definition = Ranked(Partitioned(context.Unit, 100, 114, []), RankingFunction.RowNumber);

        var (steps, rows) = await RunAsync(context, definition);

        Assert.Equal(Enumerable.Range(1, 15).Select(i => (long)i), rows.Select(r => Value(r, "Expr1002").Numeric));

        TestOutput.WriteLine($"{steps.Count} steps to return {rows.Count} rows");
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Row_Number_Restarts_At_Each_Partition()
    {
        var context = await LoadAsync();

        var definition = Ranked(Partitioned(context.Unit, 100, 114, ["Bucket"]), RankingFunction.RowNumber);

        var (_, rows) = await RunAsync(context, definition);

        Assert.Equal([1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5],
                     rows.Select(r => Value(r, "Expr1002").Numeric));
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Rank_Leaves_A_Gap_After_A_Tie_And_Dense_Rank_Does_Not()
    {
        var context = await LoadAsync();

        // The plan shape for RANK over a whole table ordered by a tying column: an outer segment on the ordering columns over an inner
        // one on the (here absent) partition columns
        var partition = new SegmentDefinition(Bucketed(context.Unit, 100, 114), "Segment1004") { NodeId = 2 };

        var value = new SegmentDefinition(partition, "Segment1005")
        {
            NodeId = 3,
            GroupBy = ["Bucket"]
        };

        var definition = new SequenceProjectDefinition(value)
        {
            NodeId = 4,
            Columns =
            [
                new RankingColumn("Expr1002", RankingFunction.Rank),
                new RankingColumn("Expr1003", RankingFunction.DenseRank),
                new RankingColumn("Expr1004", RankingFunction.RowNumber)
            ],
            PartitionColumn = "Segment1004",
            ValueColumn = "Segment1005"
        };

        var (_, rows) = await RunAsync(context, definition);

        Assert.Equal([1, 1, 1, 1, 1, 6, 6, 6, 6, 6, 11, 11, 11, 11, 11],
                     rows.Select(r => Value(r, "Expr1002").Numeric));

        Assert.Equal([1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3],
                     rows.Select(r => Value(r, "Expr1003").Numeric));

        Assert.Equal(Enumerable.Range(1, 15).Select(i => (long)i), rows.Select(r => Value(r, "Expr1004").Numeric));
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task The_Ranking_Value_Is_The_Eight_Byte_Count_The_Engine_Keeps()
    {
        var context = await LoadAsync();

        var definition = Ranked(Partitioned(context.Unit, 100, 104, []), RankingFunction.RowNumber);

        var (_, rows) = await RunAsync(context, definition);

        Assert.All(rows, r => Assert.Equal(SqlDbType.BigInt, Value(r, "Expr1002").DataType));
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task A_Missing_Segment_Column_Is_An_Error()
    {
        var context = await LoadAsync();

        var definition = new SequenceProjectDefinition(Bucketed(context.Unit, 100, 104))
        {
            NodeId = 4,
            Columns = [new RankingColumn("Expr1002", RankingFunction.RowNumber)],
            PartitionColumn = "Segment1004",
            ValueColumn = "Segment1004"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(context, definition));
    }

    private static SequenceProjectDefinition Ranked(SegmentDefinition source, RankingFunction function)
        => new(source)
        {
            NodeId = 4,
            Columns = [new RankingColumn("Expr1002", function)],
            PartitionColumn = source.SegmentColumn,
            ValueColumn = source.SegmentColumn
        };

    private static SegmentDefinition Partitioned(AllocationUnit unit, int from, int to, IReadOnlyList<string> groupBy)
        => new(Bucketed(unit, from, to), "Segment1004")
        {
            NodeId = 2,
            GroupBy = groupBy
        };

    private static ComputeScalarDefinition Bucketed(AllocationUnit unit, int from, int to)
        => new(Range(unit, Between(from, to)))
        {
            NodeId = 1,
            Columns = [new ComputedColumn("Bucket", Divide(Column("Id"), 5)) { DataType = SqlDbType.Int }]
        };

    private static AccessExpression Divide(AccessExpression left, int divisor)
        => new AccessExpression.Arithmetic(ArithmeticOperator.Divide,
                                           left,
                                           new AccessExpression.Constant(AccessValue.FromInteger(SqlDbType.Int, divisor)));

    private static AccessExpression Column(string name)
        => new AccessExpression.Column(-1, name);

    private static AccessValue Value(IRecord row, string column)
        => new RecordRowValueSource(row).GetValue(-1, column);

    private static RangeDefinition Range(AllocationUnit unit, SeekBounds bounds)
        => new(unit.AllocationUnitId, unit.RootPage, [bounds]) { NodeId = 0 };

    private static SeekBounds Between(int from, int to)
        => SeekBounds.Between(Key(from), Key(to));

    private static AccessKey Key(int value)
        => AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, value).WithColumnName("Id"));

    private sealed record Context(DatabaseSource Database, SequenceProjectIterator Service, AllocationUnit Unit);

    private static async Task<Context> LoadAsync()
    {
        var serviceHost = new TestServiceHost();

        var database = await DemoDatabase.LoadAsync(serviceHost);

        var unit = DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex);

        return new Context(database, serviceHost.GetService<SequenceProjectIterator>(), unit);
    }

    private static async Task<(List<AccessStep> Steps, List<IRecord> Rows)> RunAsync(Context context, IteratorDefinition definition)
    {
        await using var stepper = new IteratorStepper(context.Service, definition, new IteratorContext(context.Database));

        var steps = new List<AccessStep>();

        var rows = new List<IRecord>();

        while (await stepper.StepNextAsync(CancellationToken.None) is { } step)
        {
            steps.Add(step);

            if (step is AccessStep.RankRow { EmittedRecord: { } record })
            {
                rows.Add(record);
            }
        }

        return (steps, rows);
    }
}
