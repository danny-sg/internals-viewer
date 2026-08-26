using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Iterators.RowMode.Row;
using InternalsViewer.Execution.Iterators.RowMode.Stepping;
using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Row;

public class SortIteratorTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    private const string MdfPath = "./IntegrationTests/Test Data/TestDatabase.mdf";

    [RequiresFileFact(MdfPath)]
    public async Task Sorts_The_Input_Descending()
    {
        var context = await LoadAsync();

        var definition = new SortDefinition(Range(context.Unit, Between(100, 110), 0))
        {
            NodeId = 1,
            Keys = [new SortKey("Id", Descending: true)]
        };

        var (steps, values) = await RunAsync(context, definition);

        Assert.Equal(11, values.Count);
        Assert.Equal(Enumerable.Range(0, 11).Select(n => (long)(110 - n)), values);

        var sorted = Assert.Single(steps.OfType<AccessStep.Sorted>());

        Assert.Equal(11, sorted.Rows);

        TestOutput.WriteLine($"{steps.Count} steps to return {values.Count} rows");
    }

    [RequiresFileFact(MdfPath)]
    public async Task The_Sort_Blocks_Until_The_Input_Is_Exhausted()
    {
        var context = await LoadAsync();

        var definition = new SortDefinition(Range(context.Unit, Between(100, 110), 0))
        {
            NodeId = 1,
            Keys = [new SortKey("Id", Descending: false)]
        };

        var (steps, _) = await RunAsync(context, definition);

        var sortedAt = steps.FindIndex(s => s is AccessStep.Sorted);

        Assert.True(sortedAt >= 0);

        Assert.Equal(11, steps.Take(sortedAt).Count(s => s is AccessStep.SortCollect));
        Assert.DoesNotContain(steps.Take(sortedAt), s => s is AccessStep.SortRow);
        Assert.DoesNotContain(steps.Skip(sortedAt), s => s is AccessStep.SortCollect);
    }

    [RequiresFileFact(MdfPath)]
    public async Task A_Distinct_Sort_Outputs_Each_Key_Once()
    {
        var context = await LoadAsync();

        var source = new ConcatenationDefinition([Range(context.Unit, Between(100, 105), 0),
                                                  Range(context.Unit, Between(103, 108), 1)]) { NodeId = 2 };

        var definition = new SortDefinition(source)
        {
            NodeId = 3,
            Keys = [new SortKey("Id", Descending: false)],
            IsDistinct = true
        };

        var (steps, values) = await RunAsync(context, definition);

        Assert.Equal(Enumerable.Range(100, 9).Select(n => (long)n), values);

        Assert.Equal(3, steps.OfType<AccessStep.SortDuplicate>().Count());
    }

    [RequiresFileFact(MdfPath)]
    public async Task A_Top_N_Sort_Keeps_Only_The_Top_Rows()
    {
        var context = await LoadAsync();

        var definition = new SortDefinition(Range(context.Unit, Between(100, 120), 0))
        {
            NodeId = 1,
            Keys = [new SortKey("Id", Descending: false)],
            TopCount = 5
        };

        var (steps, values) = await RunAsync(context, definition);

        Assert.Equal([100, 101, 102, 103, 104], values);

        var collects = steps.OfType<AccessStep.SortCollect>().ToList();

        Assert.Equal(21, collects.Count);
        Assert.Equal(16, collects.Count(c => !c.IsRetained));

        var sorted = Assert.Single(steps.OfType<AccessStep.Sorted>());

        Assert.Equal(5, sorted.Rows);
    }

    private static RangeDefinition Range(AllocationUnit unit, SeekBounds bounds, int nodeId)
        => new(unit.AllocationUnitId, unit.RootPage, [bounds]) { NodeId = nodeId };

    private static SeekBounds Between(int from, int to)
        => SeekBounds.Between(Key(from), Key(to));

    private static AccessKey Key(int value)
        => AccessKey.Create(AccessValue.FromInteger(System.Data.SqlDbType.Int, value).WithColumnName("Id"));

    private sealed record Context(DatabaseSource Database, SortIterator Service, AllocationUnit Unit);

    private static async Task<Context> LoadAsync()
    {
        var serviceHost = new TestServiceHost();

        var connection = new FileConnectionFactory().Create(c => c.Filename = MdfPath);

        var database = await serviceHost.GetService<IDatabaseService>().LoadAsync("TestDatabase", connection, CancellationToken.None);

        var unit = DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex);

        return new Context(database, serviceHost.GetService<SortIterator>(), unit);
    }

    private static async Task<(List<AccessStep> Steps, List<long> Values)> RunAsync(Context context, IteratorDefinition definition)
    {
        await using var stepper = new IteratorStepper(context.Service, definition, new IteratorContext(context.Database));

        var steps = new List<AccessStep>();

        var values = new List<long>();

        while (await stepper.StepNextAsync(CancellationToken.None) is { } step)
        {
            steps.Add(step);

            if (step is AccessStep.SortRow { EmittedRecord: { } record })
            {
                values.Add(new RecordRowValueSource(record).GetValue(-1, "Id").Numeric);
            }
        }

        return (steps, values);
    }
}
