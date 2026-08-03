using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using System.Data;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Iterators.Joins;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Joins;

/// <summary>
/// A key lookup as the engine really performs one, seeking a nonclustered index then fetching the rest of each row from the clustered
/// index using the clustered key the nonclustered leaf carries
/// </summary>
public class KeyLookupTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Seek_Of_One_Text_Value_Looks_Up_Its_Row()
    {
        var context = await LoadAsync();

        await StartAsync(context, TextFieldEquals("Clustered table row 500"));

        var (steps, emits) = await RunAsync(context.Service);

        var emit = Assert.Single(emits);

        Assert.Equal(500, Value(emit.OuterRecord, "Id"));
        Assert.Equal(500, Value(emit.InnerRecord, "Id"));

        Assert.Equal(1, context.Service.RebindCount);

        // The lookup exists to fetch what the nonclustered index does not hold
        Assert.Contains(emit.InnerRecord!.Fields, f => f.Name == "CreatedDate");
        Assert.DoesNotContain(emit.OuterRecord!.Fields, f => f.Name == "CreatedDate");

        TestOutput.WriteLine($"{steps.Count} steps, {steps.Count(s => s is AccessStep.ReadPage)} page reads");
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Every_Outer_Row_Drives_Its_Own_Descent_From_The_Clustered_Root()
    {
        var context = await LoadAsync();

        await StartAsync(context, TextFieldBetween("Clustered table row 5000", "Clustered table row 5099"));

        var (steps, emits) = await RunAsync(context.Service);

        var outerRows = steps.Count(s => s is AccessStep.Row { EmittedRecord: not null }
                                         && s.Source == NestedLoopsStepIterator.OuterSource);

        Assert.True(outerRows > 1, "The range should cover several rows of the nonclustered index");

        Assert.Equal(outerRows, context.Service.RebindCount);
        Assert.Equal(outerRows, emits.Count);

        var rebinds = steps.OfType<AccessStep.Rebind>().ToList();

        Assert.Equal(outerRows, rebinds.Count);

        foreach (var rebind in rebinds)
        {
            var next = steps[steps.IndexOf(rebind) + 1];

            var read = Assert.IsType<AccessStep.ReadPage>(next);

            Assert.Equal(context.Inner.RootPage, read.PageAddress);
            Assert.Equal(NestedLoopsStepIterator.InnerSource, read.Source);
        }
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Each_Lookup_Returns_The_Row_Its_Outer_Key_Bound()
    {
        var context = await LoadAsync();

        await StartAsync(context, TextFieldBetween("Clustered table row 700", "Clustered table row 7099"));

        var (_, emits) = await RunAsync(context.Service);

        Assert.NotEmpty(emits);

        foreach (var emit in emits)
        {
            Assert.Equal(Value(emit.OuterRecord, "Id"), Value(emit.InnerRecord, "Id"));
        }
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task A_Lookup_Costs_One_Descent_Plus_Any_Leaf_Link_It_Follows()
    {
        var context = await LoadAsync();

        await StartAsync(context, TextFieldBetween("Clustered table row 900", "Clustered table row 9099"));

        var (steps, emits) = await RunAsync(context.Service);

        var lookups = new List<(int Reads, int LeafLinks)>();

        var reads = 0;

        var leafLinks = 0;

        foreach (var step in steps)
        {
            if (step is AccessStep.Rebind)
            {
                if (reads > 0)
                {
                    lookups.Add((reads, leafLinks));
                }

                reads = 0;
                leafLinks = 0;
            }
            else if (step.Source == NestedLoopsStepIterator.InnerSource)
            {
                if (step is AccessStep.ReadPage)
                {
                    reads++;
                }
                else if (step is AccessStep.LeafLink)
                {
                    leafLinks++;
                }
            }
        }

        lookups.Add((reads, leafLinks));

        var descents = lookups.Select(l => l.Reads - l.LeafLinks).Distinct().ToList();

        TestOutput.WriteLine($"{emits.Count} lookups, reads per lookup: {string.Join(", ", lookups.Select(l => l.Reads).Distinct().Order())}, "
                             + $"descent depth: {string.Join(", ", descents)}");

        // Every lookup descends the same balanced tree, so any read beyond that is a leaf link followed to the next page
        Assert.Single(descents);
        Assert.Equal(emits.Count, lookups.Count);
        Assert.Contains(lookups, l => l.LeafLinks > 0);
    }

    private sealed record Context(DatabaseSource Database,
                                  NestedLoopsStepIterator Service,
                                  AllocationUnit Outer,
                                  AllocationUnit Inner);

    private static async Task<Context> LoadAsync()
    {
        var serviceHost = new TestServiceHost();

        var database = await DemoDatabase.LoadAsync(serviceHost);

        return new Context(database,
                           serviceHost.GetService<NestedLoopsStepIterator>(),
                           DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.TextFieldIndex),
                           DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex));
    }

    private static async Task StartAsync(Context context, SeekBounds outerBounds)
    {
        var outerInput = new RangeDefinition(context.Outer.AllocationUnitId, context.Outer.RootPage, [outerBounds]);

        var innerInput = new SeekDefinition(context.Inner.AllocationUnitId,
                                                   context.Inner.RootPage,
                                                   [new CorrelationBinding("Id", "Id")]);

        await context.Service.OpenAsync(new IteratorContext(context.Database),
                                        new NestedLoopsDefinition(outerInput, innerInput),
                                        CancellationToken.None);
    }

    private static async Task<(List<AccessStep> Steps, List<AccessStep.JoinEmit> Emits)> RunAsync(NestedLoopsStepIterator service)
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

    private static SeekBounds TextFieldEquals(string value)
        => SeekBounds.Equality(TextKey(value));

    private static SeekBounds TextFieldBetween(string from, string to)
        => SeekBounds.Between(TextKey(from), TextKey(to));

    private static AccessKey TextKey(string value)
        => AccessKey.Create(AccessValueFactory.FromText(SqlDbType.VarChar, value).WithColumnName("TextField"));
}
