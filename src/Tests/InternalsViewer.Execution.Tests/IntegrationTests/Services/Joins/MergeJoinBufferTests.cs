using System.Data;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Services.Joins;
using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Joins;

public class MergeJoinBufferTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    private const string MdfPath = "./IntegrationTests/Test Data/TestDatabase.mdf";

    [RequiresFileFact(MdfPath)]
    public async Task Buffers_Contain_The_Rows_Being_Paired()
    {
        var context = await LoadAsync();

        await context.Service.StartAsync(context.Database,
                                         SideInput(context.Unit, Between(500, 505)),
                                         SideInput(context.Unit, Between(500, 505)),
                                         CancellationToken.None);

        var pairs = 0;

        while (await context.Service.StepNextAsync(CancellationToken.None) is { } step)
        {
            if (step is not AccessStep.JoinEmit emit)
            {
                continue;
            }

            pairs++;

            var outerBuffer = Values(context.Service.OuterBuffer);

            var innerBuffer = Values(context.Service.InnerBuffer);

            var pairedOuter = Value(emit.OuterRecord!);

            var pairedInner = Value(emit.InnerRecord!);

            TestOutput.WriteLine($"pair {emit.PairNumber}: outer=[{string.Join(",", outerBuffer)}] "
                                 + $"inner=[{string.Join(",", innerBuffer)}] paired {pairedOuter}/{pairedInner}");

            Assert.Contains(pairedOuter, outerBuffer);
            Assert.Contains(pairedInner, innerBuffer);

            // Only the rows taking part in this pairing are flagged, so the read ahead row stays unmatched
            Assert.Equal([pairedOuter], MatchedValues(context.Service.OuterBuffer));
            Assert.Equal([pairedInner], MatchedValues(context.Service.InnerBuffer));
        }

        Assert.Equal(6, pairs);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Rows_Passed_Over_Are_Never_Matched()
    {
        var context = await LoadAsync();

        await context.Service.StartAsync(context.Database,
                                         SideInput(context.Unit, Between(100, 110)),
                                         SideInput(context.Unit, Between(105, 120)),
                                         CancellationToken.None);

        while (await context.Service.StepNextAsync(CancellationToken.None) is { } step)
        {
            if (step is AccessStep.JoinEmit)
            {
                // 100 to 104 were walked past before the keys met, so only 105 is flagged
                Assert.Equal([105], MatchedValues(context.Service.OuterBuffer));

                return;
            }
        }

        Assert.Fail("No pairing was made");
    }

    [RequiresFileFact(MdfPath)]
    public async Task Buffer_Drops_Rows_The_Join_Has_Finished_With()
    {
        var context = await LoadAsync();

        await context.Service.StartAsync(context.Database,
                                         SideInput(context.Unit, Between(100, 110)),
                                         SideInput(context.Unit, Between(105, 120)),
                                         CancellationToken.None);

        var outerAtFirstPair = new List<long>();

        while (await context.Service.StepNextAsync(CancellationToken.None) is { } step)
        {
            if (step is AccessStep.JoinEmit)
            {
                outerAtFirstPair = Values(context.Service.OuterBuffer);

                break;
            }
        }

        // 100 to 104 were passed over on the way, and the join stopped holding each one as the walk moved on
        Assert.Equal([105], outerAtFirstPair);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Buffers_Drop_The_Rows_A_Pairing_Consumed()
    {
        var context = await LoadAsync();

        await context.Service.StartAsync(context.Database,
                                         SideInput(context.Unit, Between(500, 505)),
                                         SideInput(context.Unit, Between(500, 505)),
                                         CancellationToken.None);

        var seenPair = false;

        while (await context.Service.StepNextAsync(CancellationToken.None) is { } step)
        {
            if (step is AccessStep.JoinEmit)
            {
                seenPair = true;

                continue;
            }

            if (seenPair && step is AccessStep.MergeCompare)
            {
                // The pairing is done, so only the row each side read past it is carried over
                Assert.Single(context.Service.OuterBuffer);
                Assert.Single(context.Service.InnerBuffer);

                return;
            }
        }

        Assert.Fail("No comparison followed the first pairing");
    }

    [RequiresFileFact(MdfPath)]
    public async Task A_Row_The_Join_Passes_Over_Is_Not_Marked_As_Matched()
    {
        var context = await LoadAsync();

        await context.Service.StartAsync(context.Database,
                                         SideInput(context.Unit, Between(100, 110)),
                                         SideInput(context.Unit, Between(105, 120)),
                                         CancellationToken.None);

        while (await context.Service.StepNextAsync(CancellationToken.None) is { } step)
        {
            if (step is AccessStep.MergeCompare { Comparison: < 0 })
            {
                // The row is still held until the walk moves on, but the join has finished with it
                var row = Assert.Single(context.Service.OuterBuffer);

                Assert.False(row.IsMatched);
                Assert.Equal(JoinRowState.Finished, row.State);

                return;
            }
        }

        Assert.Fail("No comparison passed over an outer row");
    }

    [RequiresFileFact(MdfPath)]
    public async Task The_Row_That_Ends_A_Matched_Group_Is_Marked_As_Read_Ahead()
    {
        var context = await LoadAsync();

        await context.Service.StartAsync(context.Database,
                                         SideInput(context.Unit, Between(500, 505)),
                                         SideInput(context.Unit, Between(500, 505)),
                                         CancellationToken.None);

        var seenMatch = false;

        while (await context.Service.StepNextAsync(CancellationToken.None) is { } step)
        {
            if (step is AccessStep.MergeCompare { Comparison: 0 })
            {
                seenMatch = true;

                continue;
            }

            if (seenMatch && step is AccessStep.Row { EmittedRecord: not null, Source: MergeJoinStepService.InnerSource } row)
            {
                // Reading past the group is what proves it ended, so that row belongs to the next comparison
                Assert.True(row.IsReadAhead);

                return;
            }
        }

        Assert.Fail("No inner row followed a match");
    }

    private sealed record Context(DatabaseSource Database, MergeJoinStepService Service, AllocationUnit Unit);

    private static async Task<Context> LoadAsync()
    {
        var serviceHost = new TestServiceHost();

        var connection = new FileConnectionFactory().Create(c => c.Filename = MdfPath);

        var database = await serviceHost.GetService<IDatabaseService>()
                                        .LoadAsync("TestDatabase", connection, CancellationToken.None);

        var unit = DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex);

        return new Context(database, serviceHost.GetService<MergeJoinStepService>(), unit);
    }

    private static MergeJoinSideInput SideInput(AllocationUnit unit, SeekBounds bounds)
        => new(unit.AllocationUnitId, unit.RootPage, [bounds], ["Id"]);

    private static List<long> Values(IReadOnlyList<JoinBufferRow> buffer)
        => [.. buffer.Select(r => Value(r.Record))];

    private static List<long> MatchedValues(IReadOnlyList<JoinBufferRow> buffer)
        => [.. buffer.Where(r => r.State == JoinRowState.Matched).Select(r => Value(r.Record))];

    private static long Value(IRecord record)
        => new RecordRowValueSource(record).GetValue(-1, "Id").Numeric;

    private static SeekBounds Between(int from, int to)
        => SeekBounds.Between(Key(from), Key(to));

    private static AccessKey Key(int value)
        => AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, value).WithColumnName("Id"));
}
