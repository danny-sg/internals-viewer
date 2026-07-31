using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.Services.Joins.Inputs;
using System.Data;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Services.Heaps;
using InternalsViewer.Execution.Services.Indexes;
using InternalsViewer.Execution.Services.Joins;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Joins;

/// <summary>
/// A RID lookup, where the nonclustered index of a heap carries a row identifier instead of a clustered key so the row is fetched from
/// the page and slot it names
/// </summary>
public class RidLookupTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Lookup_Of_One_Key_Fetches_Its_Heap_Row()
    {
        var context = await LoadAsync();

        await StartAsync(context, Between(500, 500));

        var (steps, emits) = await RunAsync(context.Service);

        var emit = Assert.Single(emits);

        Assert.Equal(500, Value(emit.OuterRecord, "Id"));
        Assert.Equal(500, Value(emit.InnerRecord, "Id"));

        // The heap row holds the columns the index does not
        Assert.Contains(emit.InnerRecord!.Fields, f => f.Name == "TextField");

        TestOutput.WriteLine(string.Join("\n", steps.Select(s => $"  [{s.Source}] {s}")));
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task A_Lookup_Costs_A_Single_Page_Read_Because_A_Heap_Has_No_Tree()
    {
        var context = await LoadAsync();

        await StartAsync(context, Between(500, 900));

        var (steps, emits) = await RunAsync(context.Service);

        Assert.NotEmpty(emits);

        var readsPerLookup = new List<int>();

        var reads = 0;

        foreach (var step in steps)
        {
            if (step is AccessStep.Rebind)
            {
                if (reads > 0)
                {
                    readsPerLookup.Add(reads);
                }

                reads = 0;
            }
            else if (step is AccessStep.ReadPage && step.Source == NestedLoopsStepService.InnerSource)
            {
                reads++;
            }
        }

        readsPerLookup.Add(reads);

        TestOutput.WriteLine($"{emits.Count} lookups, page reads per lookup: {string.Join(", ", readsPerLookup.Distinct().Order())}");

        // No descent, so every lookup is one read unless the row was forwarded
        Assert.All(readsPerLookup, r => Assert.Equal(1, r));
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task The_Row_Identifier_Bound_Names_The_Page_The_Fetch_Reads()
    {
        var context = await LoadAsync();

        await StartAsync(context, Between(500, 700));

        var (steps, _) = await RunAsync(context.Service);

        var rebinds = steps.OfType<AccessStep.Rebind>().ToList();

        Assert.NotEmpty(rebinds);

        foreach (var rebind in rebinds)
        {
            Assert.NotNull(rebind.RowIdentifier);

            var next = steps[steps.IndexOf(rebind) + 1];

            var read = Assert.IsType<AccessStep.ReadPage>(next);

            Assert.Equal(rebind.RowIdentifier!.PageAddress, read.PageAddress);
            Assert.True(read.IsHeap);
        }
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Every_Index_Row_In_Range_Is_Looked_Up()
    {
        var context = await LoadAsync();

        // The heap holds every twentieth Id, so this range covers five rows
        await StartAsync(context, Between(500, 600));

        var (steps, emits) = await RunAsync(context.Service);

        var outerRows = steps.Count(s => s is AccessStep.Row { EmittedRecord: not null }
                                         && s.Source == NestedLoopsStepService.OuterSource);

        Assert.Equal(outerRows, emits.Count);
        Assert.Equal(outerRows, context.Service.RebindCount);

        Assert.Equal(Enumerable.Range(25, 6).Select(v => (long)(v * 20)), emits.Select(e => Value(e.OuterRecord, "Id")));
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task A_Fetch_That_Returns_A_Row_States_No_Verdict_Of_Its_Own()
    {
        var context = await LoadAsync();

        await StartAsync(context, Between(500, 600));

        var (steps, emits) = await RunAsync(context.Service);

        Assert.NotEmpty(emits);

        // Nothing was compared, so the emitted row is the whole story
        Assert.Empty(steps.OfType<AccessStep.JoinVerdict>());
    }

    private sealed record Context(DatabaseSource Database,
                                  NestedLoopsStepService Service,
                                  HeapFetchStepService Heap,
                                  AllocationUnit Index,
                                  AllocationUnit Heap_);

    private static async Task<Context> LoadAsync()
    {
        var serviceHost = new TestServiceHost();

        var database = await DemoDatabase.LoadAsync(serviceHost);

        return new Context(database,
                           serviceHost.GetService<NestedLoopsStepService>(),
                           serviceHost.GetService<HeapFetchStepService>(),
                           DemoDatabase.Unit(database, DemoDatabase.HeapTable, DemoDatabase.HeapIndex),
                           DemoDatabase.Unit(database, DemoDatabase.HeapTable));
    }

    private static async Task StartAsync(Context context, SeekBounds outerBounds)
    {
        var outerInput = new RangeDefinition(context.Index.AllocationUnitId, context.Index.RootPage, [outerBounds]);

        await context.Service.StartAsync(context.Database,
                                         outerInput,
                                         new RidLookupJoinInput(context.Heap),
                                         CancellationToken.None);
    }

    private static async Task<(List<AccessStep> Steps, List<AccessStep.JoinEmit> Emits)> RunAsync(NestedLoopsStepService service)
    {
        var steps = new List<AccessStep>();

        var emits = new List<AccessStep.JoinEmit>();

        while (await service.StepNextAsync(CancellationToken.None) is { } step)
        {
            steps.Add(step);

            if (step is AccessStep.JoinEmit emit)
            {
                emits.Add(emit);
            }
        }

        return (steps, emits);
    }

    private static long Value(IRecord? record, string column)
        => new RecordRowValueSource(record!).GetValue(-1, column).Numeric;

    private static SeekBounds Between(int from, int to)
        => SeekBounds.Between(Key(from), Key(to));

    private static AccessKey Key(int value)
        => AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, value).WithColumnName("Id"));
}
