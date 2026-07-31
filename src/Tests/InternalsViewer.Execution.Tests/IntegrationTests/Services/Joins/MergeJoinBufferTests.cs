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
    public async Task Buffer_Accumulates_The_Rows_Read_Toward_A_Pairing()
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

        // The outer walks 100 to 105 before the keys meet, so every row read on the way is still shown
        Assert.Equal([100, 101, 102, 103, 104, 105], outerAtFirstPair);
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

    private sealed record Context(DatabaseSource Database, MergeJoinStepService Service, AllocationUnit Unit);

    private static async Task<Context> LoadAsync()
    {
        var serviceHost = new TestServiceHost();

        var connection = new FileConnectionFactory().Create(c => c.Filename = MdfPath);

        var database = await serviceHost.GetService<IDatabaseService>()
                                        .LoadAsync("TestDatabase", connection, CancellationToken.None);

        var unit = database.AllocationUnits.Values.Single(a => a.TableName == "NumberTable_Clustered"
                                                               && a.AllocationUnitType == AllocationUnitType.InRowData);

        return new Context(database, serviceHost.GetService<MergeJoinStepService>(), unit);
    }

    private static MergeJoinSideInput SideInput(AllocationUnit unit, SeekBounds bounds)
        => new(unit.AllocationUnitId, unit.RootPage, [bounds], ["Id"]);

    private static List<long> Values(IReadOnlyList<JoinBufferRow> buffer)
        => [.. buffer.Select(r => Value(r.Record))];

    private static List<long> MatchedValues(IReadOnlyList<JoinBufferRow> buffer)
        => [.. buffer.Where(r => r.IsMatched).Select(r => Value(r.Record))];

    private static long Value(IRecord record)
        => new RecordRowValueSource(record).GetValue(-1, "Id").Numeric;

    private static SeekBounds Between(int from, int to)
        => SeekBounds.Between(Key(from), Key(to));

    private static AccessKey Key(int value)
        => AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, value).WithColumnName("Id"));
}
