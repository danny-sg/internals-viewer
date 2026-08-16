using System.Data;
using InternalsViewer.Execution.AccessPaths.Aggregation;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Iterators.Row;
using InternalsViewer.Execution.Iterators.Stepping;
using InternalsViewer.Execution.Tests.Helpers;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Row;

public class ComputeScalarIteratorTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Each_Row_Is_Passed_On_Carrying_The_Computed_Column()
    {
        var context = await LoadAsync();

        var definition = new ComputeScalarDefinition(Range(context.Unit, Between(100, 104)))
        {
            NodeId = 1,
            Columns = [new ComputedColumn("Doubled", Multiply(Column("Id"), 2))]
        };

        var (steps, rows) = await RunAsync(context, definition);

        Assert.Equal(5, rows.Count);

        Assert.Equal([200, 202, 204, 206, 208], rows.Select(r => Value(r, "Doubled").Numeric));
        Assert.Equal([100, 101, 102, 103, 104], rows.Select(r => Value(r, "Id").Numeric));

        Assert.Equal(5, steps.Count(s => s is AccessStep.ComputeRow));

        TestOutput.WriteLine($"{steps.Count} steps to return {rows.Count} rows");
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task A_Convert_Narrows_The_Value_To_The_Type_It_Names()
    {
        var context = await LoadAsync();

        var aggregate = new StreamAggregateDefinition(Range(context.Unit, Between(100, 110)))
        {
            NodeId = 1,
            Aggregates = [new AggregateColumn("Expr1010", AggregateFunction.CountStar)]
        };

        var definition = new ComputeScalarDefinition(aggregate)
        {
            NodeId = 2,
            Columns = [new ComputedColumn("Expr1005", Column("Expr1010")) { DataType = SqlDbType.Int }]
        };

        var (_, rows) = await RunAsync(context, definition);

        var row = Assert.Single(rows);

        Assert.Equal(SqlDbType.BigInt, Value(row, "Expr1010").DataType);

        Assert.Equal(SqlDbType.Int, Value(row, "Expr1005").DataType);
        Assert.Equal(11, Value(row, "Expr1005").Numeric);
    }

    private static AccessExpression Multiply(AccessExpression left, int factor)
        => new AccessExpression.Arithmetic(ArithmeticOperator.Multiply,
                                           left,
                                           new AccessExpression.Constant(AccessValue.FromInteger(SqlDbType.Int, factor)));

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

    private sealed record Context(DatabaseSource Database, ComputeScalarIterator Service, AllocationUnit Unit);

    private static async Task<Context> LoadAsync()
    {
        var serviceHost = new TestServiceHost();

        var database = await DemoDatabase.LoadAsync(serviceHost);

        var unit = DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex);

        return new Context(database, serviceHost.GetService<ComputeScalarIterator>(), unit);
    }

    private static async Task<(List<AccessStep> Steps, List<IRecord> Rows)> RunAsync(Context context, IteratorDefinition definition)
    {
        await using var stepper = new IteratorStepper(context.Service, definition, new IteratorContext(context.Database));

        var steps = new List<AccessStep>();

        var rows = new List<IRecord>();

        while (await stepper.StepNextAsync(CancellationToken.None) is { } step)
        {
            steps.Add(step);

            if (step is AccessStep.ComputeRow { EmittedRecord: { } record })
            {
                rows.Add(record);
            }
        }

        return (steps, rows);
    }
}
