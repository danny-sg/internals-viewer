using System.Data;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
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

public class FilterIteratorTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Only_Rows_Passing_The_Predicate_Are_Returned()
    {
        var context = await LoadAsync();

        var definition = new FilterDefinition(Range(context.Unit, Between(100, 109)))
        {
            NodeId = 1,
            Residual = GreaterThan("Id", 104)
        };

        var (steps, rows) = await RunAsync(context, definition);

        Assert.Equal([105, 106, 107, 108, 109], rows.Select(r => Value(r, "Id").Numeric));

        var filtered = steps.OfType<AccessStep.FilterRow>().ToList();

        Assert.Equal(10, filtered.Count);
        Assert.Equal(5, filtered.Count(f => f.Outcome == RowOutcome.Match));
        Assert.Equal(5, filtered.Count(f => f.Outcome == RowOutcome.NoMatch));

        TestOutput.WriteLine($"{steps.Count} steps to return {rows.Count} rows");
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Every_Input_Row_Is_Read_Even_When_None_Pass()
    {
        var context = await LoadAsync();

        var definition = new FilterDefinition(Range(context.Unit, Between(100, 109)))
        {
            NodeId = 1,
            Residual = GreaterThan("Id", 1000)
        };

        var (steps, rows) = await RunAsync(context, definition);

        Assert.Empty(rows);

        Assert.Equal(10, steps.Count(s => s is AccessStep.FilterRow));

        Assert.All(steps.OfType<AccessStep.FilterRow>(), f => Assert.Equal(RowOutcome.NoMatch, f.Outcome));
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task The_Passed_Count_Follows_Only_The_Rows_That_Matched()
    {
        var context = await LoadAsync();

        var definition = new FilterDefinition(Range(context.Unit, Between(100, 109)))
        {
            NodeId = 1,
            Residual = GreaterThan("Id", 104)
        };

        var (steps, _) = await RunAsync(context, definition);

        var passed = steps.OfType<AccessStep.FilterRow>().Select(f => f.PassedCount).ToList();

        Assert.Equal([0, 0, 0, 0, 0, 1, 2, 3, 4, 5], passed);
    }

    private static AccessPredicate GreaterThan(string column, int value)
        => new AccessPredicate.Comparison(new AccessExpression.Column(-1, column),
                                          ComparisonOperator.GreaterThan,
                                          new AccessExpression.Constant(AccessValue.FromInteger(SqlDbType.Int, value)));

    private static AccessValue Value(IRecord row, string column)
        => new RecordRowValueSource(row).GetValue(-1, column);

    private static RangeDefinition Range(AllocationUnit unit, SeekBounds bounds)
        => new(unit.AllocationUnitId, unit.RootPage, [bounds]) { NodeId = 0 };

    private static SeekBounds Between(int from, int to)
        => SeekBounds.Between(Key(from), Key(to));

    private static AccessKey Key(int value)
        => AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, value).WithColumnName("Id"));

    private sealed record Context(DatabaseSource Database, FilterIterator Service, AllocationUnit Unit);

    private static async Task<Context> LoadAsync()
    {
        var serviceHost = new TestServiceHost();

        var database = await DemoDatabase.LoadAsync(serviceHost);

        var unit = DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex);

        return new Context(database, serviceHost.GetService<FilterIterator>(), unit);
    }

    private static async Task<(List<AccessStep> Steps, List<IRecord> Rows)> RunAsync(Context context, IteratorDefinition definition)
    {
        await using var stepper = new IteratorStepper(context.Service, definition, new IteratorContext(context.Database));

        var steps = new List<AccessStep>();

        var rows = new List<IRecord>();

        while (await stepper.StepNextAsync(CancellationToken.None) is { } step)
        {
            steps.Add(step);

            if (step is AccessStep.FilterRow { EmittedRecord: { } record })
            {
                rows.Add(record);
            }
        }

        return (steps, rows);
    }
}
