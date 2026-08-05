using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Iterators.Row;
using InternalsViewer.Execution.Iterators.Stepping;
using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Row;

public class ConcatenationIteratorTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    private const string MdfPath = "./IntegrationTests/Test Data/TestDatabase.mdf";

    [RequiresFileFact(MdfPath)]
    public async Task Inputs_Are_Read_In_Order()
    {
        var context = await LoadAsync();

        var definition = Definition(context, Between(100, 104), Between(500, 502));

        var (steps, rows) = await RunAsync(context, definition);

        Assert.Equal(8, rows.Count);
        Assert.Equal(Enumerable.Range(1, 8).Select(n => (long)n), rows.Select(r => r.Number));
        Assert.Equal([1, 1, 1, 1, 1, 2, 2, 2], rows.Select(r => r.InputNumber));

        var stopped = steps.OfType<AccessStep.Stopped>().Last();

        Assert.Equal(2, stopped.NodeId);
        Assert.True(context.Service.IsComplete);

        TestOutput.WriteLine($"{steps.Count} steps to return {rows.Count} rows");
    }

    [RequiresFileFact(MdfPath)]
    public async Task Each_Input_Is_Announced_Before_It_Is_Read()
    {
        var context = await LoadAsync();

        var (steps, _) = await RunAsync(context, Definition(context, Between(1, 3), Between(10, 12)));

        var starts = steps.OfType<AccessStep.InputStart>().ToList();

        Assert.Equal(2, starts.Count);
        Assert.Equal([1, 2], starts.Select(s => s.Number));
        Assert.All(starts, s => Assert.Equal(2, s.Count));

        var secondStart = steps.IndexOf(starts[1]);

        Assert.DoesNotContain(steps.Take(secondStart), s => s is AccessStep.ConcatRow { InputNumber: 2 });
    }

    [RequiresFileFact(MdfPath)]
    public async Task The_Second_Input_Opens_Only_When_The_First_Is_Exhausted()
    {
        var context = await LoadAsync();

        var (steps, _) = await RunAsync(context, Definition(context, Between(1, 3), Between(10, 12)));

        var firstInputStopped = steps.FindIndex(s => s is AccessStep.Stopped && s.NodeId == 0);

        var secondInputOpened = steps.FindIndex(s => s is AccessStep.Open && s.NodeId == 1);

        Assert.True(firstInputStopped >= 0 && secondInputOpened >= 0);
        Assert.True(firstInputStopped < secondInputOpened, "the second input should open after the first has stopped");
    }

    [RequiresFileFact(MdfPath)]
    public async Task Passed_Through_Steps_Keep_The_Source_They_Arrived_With()
    {
        var context = await LoadAsync();

        var (steps, rows) = await RunAsync(context, Definition(context, Between(1, 3), Between(10, 12)));

        Assert.All(rows, r => Assert.Equal(2, r.NodeId));
        Assert.All(steps.OfType<AccessStep.Row>(), s => Assert.True(s.NodeId is 0 or 1));
    }

    private static ConcatenationDefinition Definition(Context context, SeekBounds first, SeekBounds second)
        => new([Range(context.Unit, first, 0), Range(context.Unit, second, 1)]) { NodeId = 2 };

    private static RangeDefinition Range(AllocationUnit unit, SeekBounds bounds, int nodeId)
        => new(unit.AllocationUnitId, unit.RootPage, [bounds]) { NodeId = nodeId };

    private static SeekBounds Between(int from, int to)
        => SeekBounds.Between(Key(from), Key(to));

    private static AccessKey Key(int value)
        => AccessKey.Create(AccessValue.FromInteger(System.Data.SqlDbType.Int, value).WithColumnName("Id"));

    private sealed record Context(DatabaseSource Database, ConcatenationIterator Service, AllocationUnit Unit);

    private static async Task<Context> LoadAsync()
    {
        var serviceHost = new TestServiceHost();

        var connection = new FileConnectionFactory().Create(c => c.Filename = MdfPath);

        var database = await serviceHost.GetService<IDatabaseService>().LoadAsync("TestDatabase", connection, CancellationToken.None);

        var unit = DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex);

        return new Context(database, serviceHost.GetService<ConcatenationIterator>(), unit);
    }

    private static async Task<(List<AccessStep> Steps, List<AccessStep.ConcatRow> Rows)> RunAsync(Context context,
                                                                                                  IteratorDefinition definition)
    {
        await using var stepper = new IteratorStepper(context.Service, definition, new IteratorContext(context.Database));

        var steps = new List<AccessStep>();

        var rows = new List<AccessStep.ConcatRow>();

        while (await stepper.StepNextAsync(CancellationToken.None) is { } step)
        {
            steps.Add(step);

            if (step is AccessStep.ConcatRow row)
            {
                rows.Add(row);
            }
        }

        return (steps, rows);
    }
}
