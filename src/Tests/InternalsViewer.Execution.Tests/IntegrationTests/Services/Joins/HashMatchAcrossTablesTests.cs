using System.Data;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Iterators.RowMode.Stepping;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Execution.Tests.Helpers;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Execution.Iterators.RowMode.Joins;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Joins;

/// <summary>
/// A hash match of two different tables that name a column alike, which is what a residual has to tell apart
/// </summary>
/// <remarks>
/// HeapTable holds every twentieth Id of ClusteredTable, and both carry a TextField that never holds the same text for a given Id. A
/// residual comparing the two therefore has a knowable answer, which a residual on one table against itself does not - there the two
/// sides of a matched pair are the same row and any comparison holds whichever side it was read from.
/// </remarks>
public class HashMatchAcrossTablesTests
{
    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task A_Residual_Reads_A_Column_Both_Sides_Carry_From_The_Side_That_Named_It()
    {
        var serviceHost = new TestServiceHost();

        var database = await DemoDatabase.LoadAsync(serviceHost);

        var service = serviceHost.GetService<HashMatchIterator>();

        var heap = DemoDatabase.Unit(database, DemoDatabase.HeapTable);

        var clustered = DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex);

        var build = new JoinInputDefinition(new AllocationScanDefinition(heap.FirstIamPage) { NodeId = 0 }, ["Id"]);

        var probe = new JoinInputDefinition(new RangeDefinition(clustered.AllocationUnitId,
                                                                clustered.RootPage,
                                                                [Between(100, 200)])
        {
            NodeId = 1
        },
                                            ["Id"]);

        var definition = new HashMatchDefinition(build, probe) { NodeId = 2, Residual = TextFieldsMatch() };

        var (steps, pairs) = await RunAsync(service, definition, database);

        var keyMatches = steps.OfType<AccessStep.HashCompare>().Where(c => c.IsKeyMatch).ToList();

        Assert.Equal(6, keyMatches.Count);

        Assert.All(keyMatches, c => Assert.True(c.IsResidualFail));

        Assert.Empty(pairs);
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Every_Pair_Survives_Without_The_Residual()
    {
        var serviceHost = new TestServiceHost();

        var database = await DemoDatabase.LoadAsync(serviceHost);

        var service = serviceHost.GetService<HashMatchIterator>();

        var heap = DemoDatabase.Unit(database, DemoDatabase.HeapTable);

        var clustered = DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex);

        var build = new JoinInputDefinition(new AllocationScanDefinition(heap.FirstIamPage) { NodeId = 0 }, ["Id"]);

        var probe = new JoinInputDefinition(new RangeDefinition(clustered.AllocationUnitId,
                                                                clustered.RootPage,
                                                                [Between(100, 200)])
        {
            NodeId = 1
        },
                                            ["Id"]);

        var (_, pairs) = await RunAsync(service, new HashMatchDefinition(build, probe) { NodeId = 2 }, database);

        Assert.Equal(6, pairs.Count);

        Assert.All(pairs, p => Assert.Equal(p.Build, p.Probe));
    }

    private static AccessPredicate TextFieldsMatch()
        => new AccessPredicate.Comparison(new AccessExpression.Column(-1, "TextField"),
                                          ComparisonOperator.Equal,
                                          new AccessExpression.Column(-1, "TextField"));

    private static SeekBounds Between(int from, int to)
        => SeekBounds.Between(Key(from), Key(to));

    private static AccessKey Key(int value)
        => AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, value).WithColumnName("Id"));

    private static async Task<(List<AccessStep> Steps, List<(long Build, long Probe)> Pairs)> RunAsync(HashMatchIterator service,
                                                                                                       IteratorDefinition definition,
                                                                                                       DatabaseSource database)
    {
        await using var stepper = new IteratorStepper(service, definition, new IteratorContext(database));

        var steps = new List<AccessStep>();

        var pairs = new List<(long Build, long Probe)>();

        while (await stepper.StepNextAsync(CancellationToken.None) is { } step)
        {
            steps.Add(step);

            if (step is AccessStep.JoinEmit { IsUnmatched: false, OuterRecord: { } build, InnerRecord: { } probe })
            {
                pairs.Add((new RecordRowValueSource(build).GetValue(-1, "Id").Numeric,
                           new RecordRowValueSource(probe).GetValue(-1, "Id").Numeric));
            }
        }

        return (steps, pairs);
    }
}
