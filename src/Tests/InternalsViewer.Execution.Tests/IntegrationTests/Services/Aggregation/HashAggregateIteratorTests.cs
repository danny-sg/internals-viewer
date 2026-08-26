using System.Data;
using InternalsViewer.Execution.AccessPaths.Aggregation;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Iterators.RowMode.Aggregation;
using InternalsViewer.Execution.Iterators.RowMode.Stepping;
using InternalsViewer.Execution.Tests.Helpers;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Aggregation;

public class HashAggregateIteratorTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task One_Row_Is_Returned_For_Each_Group()
    {
        var context = await LoadAsync();

        var (steps, rows) = await RunAsync(context, Grouped(context, from: 100, to: 109));

        Assert.Equal(2, rows.Count);

        Assert.Equal([20, 21], rows.Select(r => Value(r, "Bucket").Numeric).Order());
        Assert.Equal([5, 5], rows.Select(r => Value(r, "Expr1010").Numeric));

        TestOutput.WriteLine($"{steps.Count} steps to return {rows.Count} rows");
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task The_Table_Holds_One_Entry_Per_Group_Not_Per_Row()
    {
        var context = await LoadAsync();

        var (steps, _) = await RunAsync(context, Grouped(context, from: 100, to: 109));

        var groups = steps.OfType<AccessStep.HashAggregate>().ToList();

        Assert.Equal(10, groups.Count);
        Assert.Equal(2, groups.Count(g => g.IsNewGroup));

        Assert.Equal(2, context.Service.Table.RowCount);
        Assert.Equal(2, context.Service.GroupCount);
        Assert.Equal(10, context.Service.InputRowCount);
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Every_Row_Is_Read_Before_The_First_Group_Is_Returned()
    {
        var context = await LoadAsync();

        var (steps, _) = await RunAsync(context, Grouped(context, from: 100, to: 109));

        var firstEmit = steps.FindIndex(s => s is AccessStep.AggregateEmit);

        Assert.True(firstEmit >= 0);

        Assert.Equal(10, steps.Take(firstEmit).Count(s => s is AccessStep.HashAggregate));
        Assert.DoesNotContain(steps.Skip(firstEmit), s => s is AccessStep.HashAggregate);
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task A_Group_Is_Found_Again_Whatever_Order_Its_Rows_Arrive_In()
    {
        var context = await LoadAsync();

        var source = new ConcatenationDefinition([Bucketed(context.Unit, 100, 104, 0), Bucketed(context.Unit, 105, 109, 1)])
        {
            NodeId = 3
        };

        var definition = new HashAggregateDefinition(source)
        {
            NodeId = 4,
            GroupBy = ["Parity"],
            Aggregates = [new AggregateColumn("Expr1010", AggregateFunction.CountStar)]
        };

        var (_, rows) = await RunAsync(context, definition);

        Assert.Equal(2, rows.Count);

        Assert.Equal([5, 5], rows.Select(r => Value(r, "Expr1010").Numeric));
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Running_Totals_Follow_The_Group_The_Row_Landed_In()
    {
        var context = await LoadAsync();

        await using var stepper = new IteratorStepper(context.Service,
                                                      Grouped(context, from: 100, to: 109),
                                                      new IteratorContext(context.Database));

        var observed = new List<string>();

        while (await stepper.StepNextAsync(CancellationToken.None) is { } step)
        {
            if (step is AccessStep.HashAggregate aggregate)
            {
                observed.Add($"{aggregate.Key}={aggregate.GroupRows}");
            }
        }

        Assert.Equal(["20=1", "20=2", "20=3", "20=4", "20=5", "21=1", "21=2", "21=3", "21=4", "21=5"], observed);
    }

    private static HashAggregateDefinition Grouped(Context context, int from, int to)
        => new(Bucketed(context.Unit, from, to, 0))
        {
            NodeId = 2,
            GroupBy = ["Bucket"],
            Aggregates = [new AggregateColumn("Expr1010", AggregateFunction.CountStar)]
        };

    private static ComputeScalarDefinition Bucketed(AllocationUnit unit, int from, int to, int nodeId)
        => new(Range(unit, Between(from, to), nodeId))
        {
            NodeId = nodeId + 10,
            Columns =
            [
                new ComputedColumn("Bucket", Divide(Column("Id"), 5)) { DataType = SqlDbType.Int },
                new ComputedColumn("Parity", Modulo(Column("Id"), 2)) { DataType = SqlDbType.Int }
            ]
        };

    private static AccessExpression Divide(AccessExpression left, int divisor)
        => new AccessExpression.Arithmetic(ArithmeticOperator.Divide,
                                           left,
                                           new AccessExpression.Constant(AccessValue.FromInteger(SqlDbType.Int, divisor)));

    private static AccessExpression Modulo(AccessExpression left, int divisor)
        => new AccessExpression.Arithmetic(ArithmeticOperator.Modulo,
                                           left,
                                           new AccessExpression.Constant(AccessValue.FromInteger(SqlDbType.Int, divisor)));

    private static AccessExpression Column(string name)
        => new AccessExpression.Column(-1, name);

    private static AccessValue Value(IRecord row, string column)
        => new RecordRowValueSource(row).GetValue(-1, column);

    private static RangeDefinition Range(AllocationUnit unit, SeekBounds bounds, int nodeId)
        => new(unit.AllocationUnitId, unit.RootPage, [bounds]) { NodeId = nodeId };

    private static SeekBounds Between(int from, int to)
        => SeekBounds.Between(Key(from), Key(to));

    private static AccessKey Key(int value)
        => AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, value).WithColumnName("Id"));

    private sealed record Context(DatabaseSource Database, HashAggregateIterator Service, AllocationUnit Unit);

    private static async Task<Context> LoadAsync()
    {
        var serviceHost = new TestServiceHost();

        var database = await DemoDatabase.LoadAsync(serviceHost);

        var unit = DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex);

        return new Context(database, serviceHost.GetService<HashAggregateIterator>(), unit);
    }

    private static async Task<(List<AccessStep> Steps, List<IRecord> Rows)> RunAsync(Context context, IteratorDefinition definition)
    {
        await using var stepper = new IteratorStepper(context.Service, definition, new IteratorContext(context.Database));

        var steps = new List<AccessStep>();

        var rows = new List<IRecord>();

        while (await stepper.StepNextAsync(CancellationToken.None) is { } step)
        {
            steps.Add(step);

            if (step is AccessStep.AggregateEmit { EmittedRecord: { } record })
            {
                rows.Add(record);
            }
        }

        return (steps, rows);
    }
}
