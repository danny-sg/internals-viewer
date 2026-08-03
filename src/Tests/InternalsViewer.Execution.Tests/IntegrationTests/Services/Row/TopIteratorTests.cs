using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Iterators.Row;
using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Row;

public class TopIteratorTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    private const string MdfPath = "./IntegrationTests/Test Data/TestDatabase.mdf";

    [RequiresFileFact(MdfPath)]
    public async Task A_Top_Stops_The_Walk_Once_It_Has_Its_Rows()
    {
        var context = await LoadAsync();

        await OpenAsync(context, rowCount: 5);

        var (steps, rows) = await RunAsync(context.Service);

        Assert.Equal(5, rows.Count);
        Assert.Equal([1, 2, 3, 4, 5], rows.Select(r => r.Number));

        var stopped = Assert.IsType<AccessStep.Stopped>(steps[^1]);

        Assert.Equal(StopReason.RowGoalMet, stopped.Reason);
        Assert.True(context.Service.IsComplete);

        TestOutput.WriteLine($"{steps.Count} steps to return {rows.Count} rows");
    }

    /// <summary>
    /// The input is closed at the limit rather than read to the end, which is the whole point of the operator
    /// </summary>
    [RequiresFileFact(MdfPath)]
    public async Task The_Input_Is_Not_Read_Beyond_The_Limit()
    {
        var context = await LoadAsync();

        await OpenAsync(context, rowCount: 3);

        var (steps, _) = await RunAsync(context.Service);

        var readRows = steps.Count(s => s is AccessStep.Row { EmittedRecord: not null });

        Assert.Equal(3, readRows);
    }

    /// <summary>
    /// A limit no smaller than the input leaves the walk to end on its own terms
    /// </summary>
    [RequiresFileFact(MdfPath)]
    public async Task A_Limit_The_Input_Never_Reaches_Does_Not_Stop_It()
    {
        var context = await LoadAsync();

        await OpenAsync(context, rowCount: long.MaxValue, bounds: Between(1, 10));

        var (steps, rows) = await RunAsync(context.Service);

        Assert.Equal(10, rows.Count);

        var stopped = Assert.IsType<AccessStep.Stopped>(steps[^1]);

        Assert.NotEqual(StopReason.RowGoalMet, stopped.Reason);
    }

    /// <summary>
    /// Rows the input produced keep the identity of whatever read them, and only the count belongs to the TOP
    /// </summary>
    [RequiresFileFact(MdfPath)]
    public async Task Passed_Through_Steps_Keep_The_Source_They_Arrived_With()
    {
        var context = await LoadAsync();

        context.Service.IteratorId = 7;

        await OpenAsync(context, rowCount: 4, sourceNodeId: 9);

        var (steps, rows) = await RunAsync(context.Service);

        Assert.All(rows, r => Assert.Equal(7, r.Source));
        Assert.All(steps.OfType<AccessStep.Row>(), s => Assert.Equal(9, s.Source));
    }

    [RequiresFileFact(MdfPath)]
    public async Task A_Percentage_Top_Is_Refused()
    {
        var context = await LoadAsync();

        var definition = new TopDefinition(Range(context.Unit, SeekBounds.All)) { RowCount = 10, IsPercent = true };

        await Assert.ThrowsAsync<ArgumentException>(
            () => context.Service.OpenAsync(new IteratorContext(context.Database), definition, CancellationToken.None));
    }

    private static Task OpenAsync(Context context,
                                  long rowCount,
                                  SeekBounds? bounds = null,
                                  int sourceNodeId = 0)
    {
        var definition = new TopDefinition(Range(context.Unit, bounds ?? SeekBounds.All, sourceNodeId))
        {
            RowCount = rowCount
        };

        return context.Service.OpenAsync(new IteratorContext(context.Database), definition, CancellationToken.None);
    }

    private static RangeDefinition Range(AllocationUnit unit, SeekBounds bounds, int nodeId = 0)
        => new(unit.AllocationUnitId, unit.RootPage, [bounds]) { NodeId = nodeId };

    private static SeekBounds Between(int from, int to)
        => SeekBounds.Between(Key(from), Key(to));

    private static AccessKey Key(int value)
        => AccessKey.Create(AccessValue.FromInteger(System.Data.SqlDbType.Int, value).WithColumnName("Id"));

    private sealed record Context(DatabaseSource Database, TopIterator Service, AllocationUnit Unit);

    private static async Task<Context> LoadAsync()
    {
        var serviceHost = new TestServiceHost();

        var connection = new FileConnectionFactory().Create(c => c.Filename = MdfPath);

        var database = await serviceHost.GetService<IDatabaseService>().LoadAsync("TestDatabase", connection, CancellationToken.None);

        var unit = DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex);

        return new Context(database, serviceHost.GetService<TopIterator>(), unit);
    }

    private static async Task<(List<AccessStep> Steps, List<AccessStep.TopRow> Rows)> RunAsync(TopIterator service)
    {
        var steps = new List<AccessStep>();

        var rows = new List<AccessStep.TopRow>();

        while (await service.StepNextAsync(CancellationToken.None) is { } step)
        {
            steps.Add(step);

            if (step is AccessStep.TopRow row)
            {
                rows.Add(row);
            }
        }

        return (steps, rows);
    }
}
