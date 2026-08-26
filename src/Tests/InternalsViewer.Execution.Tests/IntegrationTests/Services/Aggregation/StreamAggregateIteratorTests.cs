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

public class StreamAggregateIteratorTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task A_Scalar_Aggregate_Folds_The_Whole_Input_Into_One_Row()
    {
        var context = await LoadAsync();

        var definition = new StreamAggregateDefinition(Range(context.Unit, Between(100, 110)))
        {
            NodeId = 1,
            Aggregates =
            [
                new AggregateColumn("Expr1003", AggregateFunction.Min) { Argument = Column("Id") },
                new AggregateColumn("Expr1004", AggregateFunction.Max) { Argument = Column("Id") },
                new AggregateColumn("Expr1010", AggregateFunction.CountStar)
            ]
        };

        var (steps, rows) = await RunAsync(context, definition);

        var row = Assert.Single(rows);

        Assert.Equal(100, Value(row, "Expr1003").Numeric);
        Assert.Equal(110, Value(row, "Expr1004").Numeric);
        Assert.Equal(11, Value(row, "Expr1010").Numeric);

        Assert.Equal(SqlDbType.Int, Value(row, "Expr1003").DataType);
        Assert.Equal(SqlDbType.BigInt, Value(row, "Expr1010").DataType);

        Assert.Equal(11, steps.Count(s => s is AccessStep.AggregateRow));

        TestOutput.WriteLine($"{steps.Count} steps to return {rows.Count} rows");
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task A_Scalar_Aggregate_Returns_A_Row_When_The_Input_Has_None()
    {
        var context = await LoadAsync();

        var definition = new StreamAggregateDefinition(Range(context.Unit, Between(-20, -10)))
        {
            NodeId = 1,
            Aggregates =
            [
                new AggregateColumn("Expr1003", AggregateFunction.Min) { Argument = Column("Id") },
                new AggregateColumn("Expr1010", AggregateFunction.CountStar)
            ]
        };

        var (steps, rows) = await RunAsync(context, definition);

        var row = Assert.Single(rows);

        Assert.True(Value(row, "Expr1003").IsNull);
        Assert.Equal(0, Value(row, "Expr1010").Numeric);

        Assert.DoesNotContain(steps, s => s is AccessStep.AggregateRow);
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task A_Group_Is_Returned_As_Soon_As_The_Grouping_Column_Changes()
    {
        var context = await LoadAsync();

        var (steps, rows) = await RunAsync(context, Grouped(context, from: 100, to: 109));

        Assert.Equal(2, rows.Count);

        Assert.Equal([20, 21], rows.Select(r => Value(r, "Bucket").Numeric));
        Assert.Equal([5, 5], rows.Select(r => Value(r, "Expr1010").Numeric));

        var firstEmit = steps.FindIndex(s => s is AccessStep.AggregateEmit);

        Assert.Equal(5, steps.Take(firstEmit).Count(s => s is AccessStep.AggregateRow));
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Each_Group_Starts_With_The_Totals_Reset()
    {
        var context = await LoadAsync();

        var (steps, _) = await RunAsync(context, Grouped(context, from: 100, to: 109));

        Assert.Equal(2, steps.Count(s => s is AccessStep.AggregateGroup));

        Assert.All(steps.OfType<AccessStep.AggregateEmit>(), emit => Assert.Equal(5, emit.GroupRows));
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Sum_And_Average_Keep_The_Integer_Type_Of_Their_Argument()
    {
        var context = await LoadAsync();

        var definition = new StreamAggregateDefinition(Range(context.Unit, Between(1, 10)))
        {
            NodeId = 1,
            Aggregates =
            [
                new AggregateColumn("Total", AggregateFunction.Sum) { Argument = Column("Id") },
                new AggregateColumn("Mean", AggregateFunction.Average) { Argument = Column("Id") }
            ]
        };

        var (_, rows) = await RunAsync(context, definition);

        var row = Assert.Single(rows);

        Assert.Equal(55, Value(row, "Total").Numeric);
        Assert.Equal(5, Value(row, "Mean").Numeric);

        Assert.Equal(SqlDbType.Int, Value(row, "Total").DataType);
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task The_Running_Totals_Track_The_Group_Being_Read()
    {
        var context = await LoadAsync();

        await using var stepper = new IteratorStepper(context.Service,
                                                      Grouped(context, from: 100, to: 109),
                                                      new IteratorContext(context.Database));

        var observed = new List<(string Step, string Key, string Running)>();

        while (await stepper.StepNextAsync(CancellationToken.None) is { } step)
        {
            if (step.NodeId != 2)
            {
                continue;
            }

            observed.Add((step.GetType().Name,
                          context.Service.CurrentKey,
                          string.Join(", ", context.Service.Running.Select(r => r.Value))));
        }

        foreach (var entry in observed)
        {
            TestOutput.WriteLine($"{entry.Step,-16} key={entry.Key,-4} running={entry.Running}");
        }

        var accumulates = observed.Where(o => o.Step == nameof(AccessStep.AggregateRow)).ToList();

        Assert.Equal(["1", "2", "3", "4", "5", "1", "2", "3", "4", "5"], accumulates.Select(a => a.Running));

        Assert.Equal(["20", "20", "20", "20", "20", "21", "21", "21", "21", "21"], accumulates.Select(a => a.Key));
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task The_Group_Column_Values_Follow_The_Group_Being_Read()
    {
        var context = await LoadAsync();

        await using var stepper = new IteratorStepper(context.Service,
                                                      Grouped(context, from: 100, to: 109),
                                                      new IteratorContext(context.Database));

        var observed = new List<string>();

        while (await stepper.StepNextAsync(CancellationToken.None) is { } step)
        {
            if (step is AccessStep.AggregateRow { NodeId: 2 })
            {
                var group = Assert.Single(context.Service.GroupValues);

                Assert.Equal("Bucket", group.Column);

                observed.Add(group.Value);
            }
        }

        Assert.Equal(["20", "20", "20", "20", "20", "21", "21", "21", "21", "21"], observed);
    }

    private static StreamAggregateDefinition Grouped(Context context, int from, int to)
        => new(Bucketed(context.Unit, from, to))
        {
            NodeId = 2,
            GroupBy = ["Bucket"],
            Aggregates = [new AggregateColumn("Expr1010", AggregateFunction.CountStar)]
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

    private sealed record Context(DatabaseSource Database, StreamAggregateIterator Service, AllocationUnit Unit);

    private static async Task<Context> LoadAsync()
    {
        var serviceHost = new TestServiceHost();

        var database = await DemoDatabase.LoadAsync(serviceHost);

        var unit = DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex);

        return new Context(database, serviceHost.GetService<StreamAggregateIterator>(), unit);
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
