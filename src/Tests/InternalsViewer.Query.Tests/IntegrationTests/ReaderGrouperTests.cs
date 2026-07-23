using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Parsing;
using InternalsViewer.Query.Tests.Helpers;
using InternalsViewer.TransactionLog;
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

    [Fact]
    public async Task Diagnose_Ungrouped_Physical_Reads()
    {
        var results = await RunQuery("SELECT * FROM dbo.ClusteredTable WITH (TABLOCKX)");

        // Top level plus group members — a FileEvent that became a spine is replaced by its group at the top level.
        var allEvents = results.EngineEvents
                               .SelectMany(e => e is ReadEventGroup g ? g.Events.Prepend(e) : [e])
                               .ToList();

        TestOutputHelper.WriteLine("=== event name counts (all, incl. group members) ===");

        foreach (var g in allEvents.GroupBy(e => e.Name).OrderByDescending(g => g.Count()))
        {
            TestOutputHelper.WriteLine($"{g.Count(),6}  {g.Key}");
        }

        var fileEvents = allEvents.OfType<FileEvent>().Where(f => f.IsRead).ToList();

        TestOutputHelper.WriteLine("");
        TestOutputHelper.WriteLine($"=== file reads: {fileEvents.Count} ===");

        foreach (var f in fileEvents)
        {
            TestOutputHelper.WriteLine($"{f.TimeUs}:{f.DurationUs} Size={f.Size} ({f.Size / 8192} pages) "
                + $"{f.FromPageAddress}-{f.ToPageAddress}");
        }

        TestOutputHelper.WriteLine("");
        TestOutputHelper.WriteLine("=== group counts by type ===");

        foreach (var g in results.EngineEvents.OfType<ReadEventGroup>().GroupBy(g => g.ReadType))
        {
            TestOutputHelper.WriteLine($"{g.Key,-9} groups={g.Count()} pages={g.Sum(x => x.PageCount)}");
        }

        var cachedPages = results.EngineEvents.OfType<ReadEventGroup>()
                                 .Where(g => g.ReadType == ReadType.Cached)
                                 .SelectMany(g => g.Pages)
                                 .Where(p => p.PageId is >= 960 and <= 998)
                                 .Distinct()
                                 .Count();

        TestOutputHelper.WriteLine($"cached groups covering pages 960-998: {cachedPages}");

        TestOutputHelper.WriteLine("");
        TestOutputHelper.WriteLine("=== non-cached read groups: composition ===");

        foreach (var g in results.EngineEvents.OfType<ReadEventGroup>().Where(g => g.ReadType == ReadType.NonCached))
        {
            TestOutputHelper.WriteLine($"{g.TimeUs}:{g.DurationUs} {g.Description} pages={g.PageCount}");

            foreach (var m in g.Events)
            {
                var kind = m is FileEvent fe ? $"FileEvent {fe.FromPageAddress}-{fe.ToPageAddress}" : m.GetType().Name;

                TestOutputHelper.WriteLine($"    {m.TimeUs}:{m.DurationUs} [{kind}] {m.Name} - {m.Description}");
            }
        }

        var grouped = results.EngineEvents.OfType<ReadEventGroup>().SelectMany(g => g.Events).ToHashSet();

        var ungrouped = results.EngineEvents.OfType<IoEvent>().Where(io => io.IsRead && !grouped.Contains(io)).ToList();

        TestOutputHelper.WriteLine("");
        TestOutputHelper.WriteLine($"=== ungrouped physical_page_reads: {ungrouped.Count} ===");

        foreach (var io in ungrouped)
        {
            // Which read ranges (if any) cover this page — empty means nothing could ever have grouped it.
            var covering = fileEvents.Where(f => io.PageAddress is { } p
                                                 && p.FileId == f.FromPageAddress.FileId
                                                 && p.PageId >= f.FromPageAddress.PageId
                                                 && p.PageId < f.ToPageAddress.PageId)
                                     .ToList();

            var cover = covering.Count == 0
                ? "NO COVERING FILE READ"
                : string.Join(", ", covering.Select(f => $"{f.FromPageAddress}-{f.ToPageAddress}@{f.TimeUs}"));

            TestOutputHelper.WriteLine($"{io.TimeUs} {io.PageAddress} -> {cover}");
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
