using InternalsViewer.Internals;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Parsing.Statements;
using InternalsViewer.Query.Tests.Helpers;
using InternalsViewer.TransactionLog;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace InternalsViewer.Query.Tests.IntegrationTests;

[Trait("Category", "Integration")]
public class PoolMissDiagnosticTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Dump_Pool_Misses_And_Linked_Reads()
    {
        var builder = new SqlConnectionStringBuilder(ConnectionStringHelper.GetConnectionString("Local"))
        {
            InitialCatalog = "ColumnStoreLab"
        };

        var connectionString = builder.ConnectionString;

        using var host = Host.CreateDefaultBuilder().ConfigureServices((_, s) => s.RegisterServices()).Build();

        var connection = new ServerConnectionFactory(TestLogger.GetLogger<QueryPageReader>(output))
            .Create(c => c.ConnectionString = connectionString);

        var database = await host.Services.GetRequiredService<IDatabaseService>()
                                 .LoadAsync("ColumnStoreLab", connection, CancellationToken.None);

        var runner = new QueryRunner(TestLogger.GetLogger<QueryRunner>(output, LogLevel.Warning),
                                     new EventReader(TestLogger.GetLogger<EventReader>(output, LogLevel.Warning)),
                                     new LogRecordReader(TestLogger.GetLogger<LogRecordReader>(output, LogLevel.Warning)),
                                     host.Services.GetRequiredService<ColumnstoreService>(),
                                     host.Services.GetRequiredService<ColumnstorePageMapper>());

        var payload = new ExecuteSqlPayload("SELECT * FROM Sales", new QueryOptions { DisableReadAhead = false }, StatementType.Select, null);

        var result = await runner.TraceQuery(payload, database, new EventOptions { IncludeCallStack = false }, @"C:\Symbols", null,
                                             CancellationToken.None);

        Dump(result.EngineEvents);
    }

    [Fact]
    public async Task Dump_Saved_Trace()
    {
        var builder = new SqlConnectionStringBuilder(ConnectionStringHelper.GetConnectionString("Local"))
        {
            InitialCatalog = "ColumnStoreLab"
        };

        var connectionString = builder.ConnectionString;

        using var host = Host.CreateDefaultBuilder().ConfigureServices((_, s) => s.RegisterServices()).Build();

        var connection = new ServerConnectionFactory(TestLogger.GetLogger<QueryPageReader>(output))
            .Create(c => c.ConnectionString = connectionString);

        var database = await host.Services.GetRequiredService<IDatabaseService>()
                                 .LoadAsync("ColumnStoreLab", connection, CancellationToken.None);

        var reader = new EventReader(TestLogger.GetLogger<EventReader>(output, LogLevel.Warning));

        var path = @"C:\ProgramData\InternalsViewer\Traces\QueryReplay_05f9b1f209e948aea098245fe188f362_0_134342016891580000.xel";

        var (events, _, callStack) = await reader.GetEvents(path, connectionString, database, false, null, CancellationToken.None);

        var unit = database.AllocationUnits.Values.First(a => a.TableName == "Sales"
                                                             && a.AllocationUnitType == InternalsViewer.Internals.Engine.Database.Enums.AllocationUnitType.InRowData);

        var index = await host.Services.GetRequiredService<ColumnstoreService>().GetIndex(unit, database, CancellationToken.None);

        var reads = await host.Services.GetRequiredService<ColumnstorePageMapper>().MapAsync(database, index, CancellationToken.None);

        ObjectPoolPageLinker.Link(events, index.HobtId, reads, index.DeleteBitmapAllocationUnit);

        events = EventFilter.Filter(events, new EventOptions { IncludeCallStack = true });

        await InternalsViewer.Query.CallStack.CallstackProcessor.Process(callStack, @"C:\Symbols", null, CancellationToken.None);

        InternalsViewer.Query.CallStack.OperatorCallStackMatcher.Match(events);

        InternalsViewer.Query.CallStack.CallStackPlanNodeMatcher.Match(events);

        ObjectPoolReadLinker.Link(events);

        InternalsViewer.Query.Events.Consolidation.ObjectPoolDurationStamper.Stamp(events);

        Dump(events);
    }

    private void Dump(List<EngineEvent> source)
    {
        var events = source.OrderBy(e => e.SequenceId).ToList();

        var origin = events.Min(e => e.TimeUs);

        DumpReadAhead(events, origin);

        foreach (var e in events)
        {
            switch (e)
            {
                case ObjectPoolEvent pool:
                    var linked = events.OfType<ReadEventGroup>().Where(r => ReferenceEquals(r.PoolLookup, pool)).ToList();

                    var before = linked.Where(r => r.SequenceId < pool.SequenceId).ToList();

                    foreach (var r in linked.OrderBy(r => r.SequenceId).Take(6))
                    {
                        output.WriteLine($"       read seq={r.SequenceId} t={(r.TimeUs - origin) / 1000.0:F3} type={r.ReadType} task={r.TaskAddress:X} worker={r.WorkerAddress:X} thread={r.ThreadId} pages={string.Join(",", r.Pages.Select(p => p.PageId))} members={string.Join(",", r.Events.Select(m => m.GetType().Name))}");
                    }

                    output.WriteLine($"       pool task={pool.TaskAddress:X} worker={pool.WorkerAddress:X} thread={pool.ThreadId}");

                    if (pool.ObjectType == InternalsViewer.Query.Events.BatchMode.Enums.ColumnStoreObjectType.ColumnSegment && pool.ColumnId == 2)
                    {
                        foreach (var r in linked.Where(r => r.ReadType == ReadType.NonCached).OrderBy(r => r.SequenceId).Take(2)
                                                .Concat(linked.OrderBy(r => r.SequenceId).TakeLast(1)))
                        {
                            output.WriteLine($"     STACK read seq={r.SequenceId} t={(r.TimeUs - origin) / 1000.0:F3} type={r.ReadType} pages={r.PageCount}");

                            foreach (var member in r.Events.Take(3))
                            {
                                output.WriteLine($"       member {member.GetType().Name} {member.Name}: {Stack(member)}");
                            }

                            output.WriteLine($"       group: {Stack(r)}");
                        }
                    }

                    output.WriteLine($"     linked={linked.Count} before={before.Count} after={linked.Count - before.Count} "
                                     + (linked.Count > 0 ? $"firstRead t={(linked.Min(r => r.TimeUs) - origin) / 1000.0:F3} seq={linked.Min(r => r.SequenceId)} lastRead t={(linked.Max(r => r.TimeUs + r.DurationUs) - origin) / 1000.0:F3}" : ""));

                    output.WriteLine($"POOL seq={pool.SequenceId,6} t={(pool.TimeUs - origin) / 1000.0,9:F3} dur={pool.DurationUs / 1000.0,8:F3} "
                                     + $"hit={pool.IsHit} type={pool.ObjectType} col={pool.ColumnId} obj={pool.PoolObjectId} rg={pool.RowGroupId} "
                                     + $"pages={pool.Pages.Count} {(pool.Pages.Count > 0 ? $"{pool.Pages.Min(p => p.PageId)}-{pool.Pages.Max(p => p.PageId)}" : "")}");
                    break;
                case SegmentScanEvent scan:
                    output.WriteLine($"SCAN seq={scan.SequenceId,6} t={(scan.TimeUs - origin) / 1000.0,9:F3} dur={scan.DurationUs / 1000.0,8:F3} "
                                     + $"rg={scan.RowGroupId} col={scan.ColumnId}");
                    break;
                case ColumnStoreScanEvent cs:
                    output.WriteLine($"CS   seq={cs.SequenceId,6} t={(cs.TimeUs - origin) / 1000.0,9:F3} dur={cs.DurationUs / 1000.0,8:F3} {cs.EventName} rg={cs.RowGroupId}");
                    break;
            }
        }
    }

    private void DumpReadAhead(List<EngineEvent> events, long origin)
    {
        var owners = new Dictionary<InternalsViewer.Internals.Engine.Address.PageAddress, ObjectPoolEvent>();

        foreach (var pool in events.OfType<ObjectPoolEvent>())
        {
            foreach (var page in pool.Pages)
            {
                owners.TryAdd(page, pool);
            }
        }

        var ahead = events.OfType<ReadEventGroup>().Where(r => r.IsReadAhead).ToList();

        output.WriteLine($"READ AHEAD {ahead.Count} of {events.OfType<ReadEventGroup>().Count()} reads, linked to pool {events.OfType<ReadEventGroup>().Count(r => r.PoolLookup is not null)}, read ahead for a lookup {ahead.Count(r => r.ReadAheadFor is not null)}");

        foreach (var group in ahead.GroupBy(r => string.Join(",", r.Pages.Select(p => owners.TryGetValue(p, out var o) ? $"rg{o.RowGroupId}" : "none").Distinct().Order())))
        {
            output.WriteLine($"  owners={group.Key,-12} reads={group.Count(),4} pages={group.Sum(r => r.PageCount),5} "
                             + $"t={(group.Min(r => r.TimeUs) - origin) / 1000.0:F3}..{(group.Max(r => r.TimeUs) - origin) / 1000.0:F3} "
                             + $"withNode={group.Count(r => r.PlanNodeIdentifier is not null)}");
        }
    }

    private static string Stack(EngineEvent e)
    {
        var names = new List<string>();

        for (var node = e.CallStack; node is not null && names.Count < 40; node = node.Parent)
        {
            if (node.Frame?.Resolved is { } f)
            {
                names.Add($"{f.ClassName}::{f.MethodName}");
            }
        }

        return names.Count == 0 ? "(none)" : string.Join(" < ", names);
    }
}
