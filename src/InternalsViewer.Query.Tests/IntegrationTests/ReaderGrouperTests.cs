using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Parsing;
using InternalsViewer.Query.Tests.Helpers;
using InternalsViewer.Query.TransactionLog;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Latches;

namespace InternalsViewer.Query.Tests.IntegrationTests;

public class ReaderGrouperTests(ITestOutputHelper testOutputHelper)
{
    public ITestOutputHelper TestOutputHelper { get; } = testOutputHelper;

    [Fact]
    public async Task Are_Read_Events_Grouped_For_Heap()
    {
        var query = "SELECT TOP 100 * FROM dbo.HeapTable";

        await TestQuery(query);
    }

    [Fact]
    public async Task Are_Read_Events_Grouped_For_BTree()
    {
        var query = "SELECT TOP 10 * FROM dbo.ClusteredTable";

        await TestQuery(query);
    }

    [Fact]
    public async Task Diagnose_KP_Reads_And_Physical_Reads()
    {
        var results = await RunQuery("SELECT TOP 100 * FROM dbo.HeapTable");

        TestOutputHelper.WriteLine("=== grouped reads: composition ===");

        foreach (var g in results.EngineEvents.OfType<ReadEventGroup>())
        {
            var m = g.Events;

            var hasPhysical = m.Any(e => e.Name == "physical_page_read");
            var hasFileRead = m.Any(e => e.Name == "file_read");
            var ex = m.Count(e => e is LatchEvent { LatchMode: Events.Latches.LatchMode.EX });
            var sh = m.Count(e => e is LatchEvent { LatchMode: Events.Latches.LatchMode.SH });
            var kp = m.Count(e => e is LatchEvent { LatchMode: Events.Latches.LatchMode.KP });

            var flag = kp > 0 && !hasPhysical ? "   <-- KP but NO physical_page_read" : "";

            TestOutputHelper.WriteLine($"{g.ReadType,-9} {g.Description,-34} phys={hasPhysical,-5} file={hasFileRead,-5} "
                + $"EX={ex} SH={sh} KP={kp}{flag}");
        }

        var grouped = results.EngineEvents.OfType<ReadEventGroup>().SelectMany(g => g.Events).ToHashSet();

        var bareKp = results.EngineEvents.OfType<LatchEvent>()
            .Where(l => l.LatchMode == Events.Latches.LatchMode.KP && !grouped.Contains(l))
            .ToList();

        TestOutputHelper.WriteLine("");
        TestOutputHelper.WriteLine($"=== bare BUF KP latches (not grouped): {bareKp.Count} ===");

        foreach (var latch in bareKp)
        {
            TestOutputHelper.WriteLine($"{latch.TimeUs} bare KP {latch.PageAddress}");
        }
    }

    private async Task TestQuery(string sql)
    {
        var results = await RunQuery(sql);

        foreach (var e in results.EngineEvents)
        {
            var detail = e is FileEvent f ? $" [Mode={f.Mode} Size={f.Size} {f.FromPageAddress}-{f.ToPageAddress}]" : "";

            TestOutputHelper.WriteLine($"{e.TimeUs}:{e.DurationUs} {e.Name} - {e.Description}{detail}  OBJ=[{e.ObjectName}]");

            if (e is ReadEventGroup grouped)
            {
                foreach (var g in grouped.Events)
                {
                    TestOutputHelper.WriteLine($"    {g.TimeUs}:{g.DurationUs} {g.Name} - {g.Description}");
                }
            }
        }
    }

    private async Task<QueryResult> RunQuery(string sql)
    {
        var logger = TestLogger.GetLogger<QueryRunner>(TestOutputHelper, LogLevel.Information);

        var connectionString = ConnectionStringHelper.GetConnectionString("Local");

        var eventReader = new EventReader(TestLogger.GetLogger<EventReader>(TestOutputHelper, LogLevel.Information));

        var logReader = new LogRecordReader(TestLogger.GetLogger<LogRecordReader>(TestOutputHelper, LogLevel.Information));
        var executor = new QueryRunner(logger, eventReader, logReader);

        var database = new DatabaseSource(
            new ServerConnectionFactory(TestLogger.GetLogger<QueryPageReader>(TestOutputHelper))
                .Create(c => c.ConnectionString = connectionString))
        {
            Name = "TestDatabase"
        };

        var payload = new ExecuteSqlPayload(sql, new QueryOptions(), StatementType.Select, null);

        var result = await executor.TraceQuery(payload, database, new EventOptions(), @"C:\Symbols", null,
            CancellationToken.None);

        return result;
    }
}
