using System.IO.Compression;
using System.Text.Json.Nodes;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.TimeTravel;
using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Iterators;
using InternalsViewer.Query.CallStack.TimeTravel.Locators;
using InternalsViewer.Query.CallStack.TimeTravel.Memory;
using InternalsViewer.Query.CallStack.TimeTravel.Native;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Splits;
using InternalsViewer.Query.Events.Transactions;
using InternalsViewer.Query.Events.Waits;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Parsers;
using InternalsViewer.Query.Results;
using InternalsViewer.TransactionLog;
using InternalsViewer.TransactionLog.LogRecords;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class RunExportTests
{
    private const ulong Scan = 0x9000;

    private const ulong Page336 = 0x0000_0001_0000_0150;

    private const string PlanXml =
        """
        <?xml version="1.0"?>
        <ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan" Version="1.7" Build="16.0.0">
          <BatchSequence>
            <Batch>
              <Statements>
                <StmtSimple StatementText="SELECT * FROM dbo.Orders" StatementType="SELECT" StatementSubTreeCost="0.0032875">
                  <QueryPlan>
                    <RelOp NodeId="0" PhysicalOp="Clustered Index Scan" LogicalOp="Clustered Index Scan"
                           EstimatedTotalSubtreeCost="0.0032875" EstimateRows="5" AvgRowSize="11" Parallel="0">
                      <OutputList>
                        <ColumnReference Database="[Sales]" Schema="[dbo]" Table="[Orders]" Column="OrderId"/>
                      </OutputList>
                      <IndexScan Ordered="0" ForcedIndex="0" NoExpandHint="0" Storage="RowStore">
                        <Object Database="[Sales]" Schema="[dbo]" Table="[Orders]" Index="[PK_Orders]" IndexKind="Clustered"/>
                      </IndexScan>
                    </RelOp>
                  </QueryPlan>
                </StmtSimple>
              </Statements>
            </Batch>
          </BatchSequence>
        </ShowPlanXML>
        """;

    private static AllocationUnit Orders { get; } = new()
    {
        AllocationUnitId = 72057594043236352,
        ObjectId = 5,
        IndexId = 1,
        SchemaName = "dbo",
        TableName = "Orders",
        IndexName = "PK_Orders",
        DisplayName = "dbo.Orders.PK_Orders"
    };

    [Fact]
    public async Task Exporting_An_Imported_Run_Writes_The_Same_File()
    {
        var first = await Export(Content());

        var second = await Export(await Import(first));

        var firstEntries = Entries(first);

        var secondEntries = Entries(second);

        Assert.Equal(firstEntries.Keys.Order(), secondEntries.Keys.Order());

        foreach (var (name, content) in firstEntries)
        {
            Assert.True(content.SequenceEqual(secondEntries[name]), $"{name} differs after a round trip");
        }
    }

    [Fact]
    public async Task Imported_Events_Keep_Their_Shared_References()
    {
        var imported = await Import(await Export(Content()));

        var events = imported.Result.EngineEvents;

        var read = Assert.Single(events.OfType<ReadEventGroup>());

        var latch = Assert.IsType<LatchEvent>(read.Events[0]);

        var wait = Assert.IsType<WaitEvent>(read.Events[2]);

        var transactionLog = Assert.Single(events.OfType<TransactionLogEvent>());

        var split = Assert.Single(events.OfType<PageSplitEvent>());

        var scan = Assert.Single(events.OfType<ExecutionOperatorEvent>());

        Assert.Same(Orders, latch.AllocationUnit);
        Assert.Same(latch, Assert.Single(latch.CallStack!.Events));
        Assert.Equal(9, wait.FoldedFrom!.TimeUs);
        Assert.Same(transactionLog.LogRecord, split.LogRecords[0]);
        Assert.Same(transactionLog.LogRecord, imported.Result.LogRecords[0]);
        Assert.Equal(new RowIdentifier(1, 336, 4), transactionLog.RowIdentifier);
        Assert.Same(AllocationUnit.Unknown, events.Single(e => e.Name == "custom_event").AllocationUnit);
        Assert.Same(imported.FullTrace!.CallStack.Root, scan.EntryFrames[0].Parent!.Parent);
        Assert.Equal(100ul, scan.Memory!.Allocated);
    }

    [Fact]
    public async Task An_Imported_Full_Trace_Has_Its_Calls_Spans_And_Memory()
    {
        var imported = await Import(await Export(Content()));

        var fullTrace = imported.FullTrace!;

        var calls = fullTrace.CallLog.CallsOf(0x30, Scan)!;

        var getRow = fullTrace.CallStack.Root.ChildNodes.Single().ChildNodes.Single();

        Assert.Equal(2, calls.Count);
        Assert.Equal(6ul, calls[1].IntegerSlots[0]);
        Assert.Same(getRow, fullTrace.CallLog.NodeOf(calls[1]));
        Assert.Equal(new PageAddress(1, 336), calls[1].LocatorAt(2)?.Page);
        Assert.Equal("(1:336), (1:336:4)", getRow.LocatorsDetail);
        Assert.Equal(3, fullTrace.Timeline.SpanCount);
        Assert.Equal(100ul, fullTrace.Timeline.PeakInUseDuring(0, 100));
        Assert.Single(fullTrace.Timeline.Lifetimes);
    }

    [Fact]
    public async Task A_Node_Removed_From_The_Full_Trace_Tree_Stays_Out_Of_It_On_Import()
    {
        var fullTrace = (await Import(await Export(Content()))).FullTrace!;

        var publish = fullTrace.Timeline.NodeOf(2)!;

        Assert.Equal("XeSqlPkg::lock_acquired::Publish", publish.Symbol);
        Assert.Same(fullTrace.Timeline.NodeOf(0), publish.Parent);
        Assert.DoesNotContain(publish, fullTrace.CallStack.Nodes());
    }

    [Fact]
    public async Task An_Imported_Plan_Keeps_Its_Handle_Batch_Info_And_Columns()
    {
        var imported = await Import(await Export(Content()));

        var plan = Assert.Single(imported.Result.ExecutionPlans);

        var column = Assert.Single(Assert.Single(imported.Result.ResultSets).Columns);

        Assert.Equal(3, plan.PlanHandleId);
        Assert.Equal(PlanXml, plan.Xml);
        Assert.Equal(12, plan.NodesById[0].BatchInfo!.BatchCount);
        Assert.Equal(("OrderId", typeof(int)), (column.Name, column.ClrType));
        Assert.Empty(imported.Result.ResultSets[0].Rows);
        Assert.Equal("SELECT * FROM dbo.Orders", imported.SqlText);
        Assert.Equal(["Running Query - Completed in 00:00:01"], imported.Messages);
    }

    private static async Task<byte[]> Export(RunExportContent content)
    {
        using var stream = new MemoryStream();

        await RunExport.WriteAsync(stream, content, CancellationToken.None);

        return stream.ToArray();
    }

    private static async Task<RunExportContent> Import(byte[] export)
    {
        using var stream = new MemoryStream(export);

        return await RunExport.ReadAsync(stream, id => id == Orders.AllocationUnitId ? Orders : null, CancellationToken.None);
    }

    private static Dictionary<string, byte[]> Entries(byte[] export)
    {
        using var zip = new ZipArchive(new MemoryStream(export), ZipArchiveMode.Read);

        var entries = new Dictionary<string, byte[]>();

        foreach (var entry in zip.Entries)
        {
            using var stream = entry.Open();

            using var copy = new MemoryStream();

            stream.CopyTo(copy);

            entries[entry.FullName] = entry.FullName == RunExport.ManifestEntry ? WithoutExportTime(copy.ToArray()) : copy.ToArray();
        }

        return entries;
    }

    private static byte[] WithoutExportTime(byte[] manifest)
    {
        var json = JsonNode.Parse(manifest)!.AsObject();

        json.Remove("ExportedAt");

        return System.Text.Encoding.UTF8.GetBytes(json.ToJsonString());
    }

    private static RunExportContent Content()
    {
        var tree = new CallStackTree();

        var latch = new LatchEvent
        {
            Name = "latch_acquired",
            TimeUs = 10,
            PageAddress = new PageAddress(1, 336),
            LatchAddress = 0xABC,
            LatchMode = (LatchMode)2,
            AllocationUnit = Orders
        };

        latch.CallStack = tree.Add([Frame(0x2000, "BUF::AcquireLatch+0x10"), Frame(0x1234, "CQScanRowsetNew::GetRow+0x24")], latch);

        var io = new IoEvent { Name = "file_read_completed", TimeUs = 12, PageAddress = new PageAddress(1, 336), IsRead = true, IsRoot = true };

        var wait = new WaitEvent
        {
            Name = "wait_info",
            TimeUs = 11,
            WaitType = WaitType.LCK_M_SCH_S,
            IsEnd = true,
            FoldedFrom = new WaitEvent { Name = "wait_info", TimeUs = 9 }
        };

        var read = new ReadEventGroup
        {
            Name = "Read",
            TimeUs = 10,
            Events = [latch, io, wait],
            Pages = [new PageAddress(1, 336), new PageAddress(1, 337)],
            ReadType = ReadType.NonCached,
            AllocationUnit = Orders
        };

        var lockEvent = new LockEvent
        {
            Name = "lock_acquired",
            TimeUs = 14,
            LockMode = (LockMode)3,
            Resource = new LockResource { ResourceType = (LockResourceType)7, ObjectId = 5, RowIdentifier = new RowIdentifier(1, 336, 4) },
            AllocationUnit = Orders
        };

        var insert = new InsertRowsLogRecord
        {
            Lsn = new LogSequenceNumber(40, 100, 2),
            PreviousLsn = new LogSequenceNumber(40, 90, 1),
            Operation = LogOperation.LOP_INSERT_ROWS,
            PageAddress = new PageAddress(1, 336),
            SlotId = 4,
            RowData = [1, 2, 3]
        };

        var scan = new ExecutionOperatorEvent
        {
            Name = "Clustered Index Scan",
            OperatorDescription = "Clustered Index Scan",
            PlanNodeIdentifier = new PlanNodeIdentifier { PlanHandleId = 1, NodeId = 0 },
            Threads = [new OperatorThread(0, 10, 20, 5)],
            Instances = [Scan],
            Memory = new TimeTravelMemoryPurpose("Clustered Index Scan", 100, 2, 50, 80, [new TimeTravelMemoryUse("GetRow", "new", 100, 2)])
        };

        var fullTrace = FullTrace(scan);

        var plan = ExecutionPlanParser.Parse(PlanXml, (short)3);

        plan.NodesById[0].BatchInfo = new BatchInfo { BatchCount = 12 };

        var result = new QueryResult
        {
            SessionId = "QueryReplay_0123456789abcdef0123456789abcdef",
            IsSuccess = true,
            EngineEvents =
            [
                read,
                new LockGroup { Name = "Locks", TimeUs = 14, Events = [lockEvent] },
                new TransactionLogEvent { Name = "transaction_log", TimeUs = 20, Operation = LogOperation.LOP_INSERT_ROWS, LogRecord = insert },
                new PageSplitEvent { Name = "page_split", TimeUs = 21, PageAddress = new PageAddress(1, 336), LogRecords = [insert] },
                new EngineEvent { Name = "custom_event", TimeUs = 30, AllocationUnit = AllocationUnit.Unknown },
                scan
            ],
            ExecutionPlans = [plan],
            CallStackTree = tree,
            ResultSets = [new QueryResultSet { Columns = [new ResultColumn(0, "OrderId", typeof(int), false)] }],
            RowCount = 5,
            CropStartUs = 10,
            CropEndUs = 30,
            LogRecords = [insert]
        };

        return new RunExportContent
        {
            ServerName = ".\\SQL2025",
            DatabaseName = "Sales",
            SqlText = "SELECT * FROM dbo.Orders",
            Messages = ["Running Query - Completed in 00:00:01"],
            Result = result,
            FullTrace = fullTrace
        };
    }

    private static FullTraceResult FullTrace(ExecutionOperatorEvent scan)
    {
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10, "CQueryScan::GetRow+0x0", 0), 1);

        var getRow = tree.AddCall(caller, Frame(0x30, "CQScanRowsetNew::GetRow+0x0", Scan), 2);

        getRow.Locators = new TimeTravelLocatorSet();

        getRow.Locators.Add(new TimeTravelLocator(0, LocatorKind.Page, Page336));

        getRow.Locators.Add(new TimeTravelLocator(0, LocatorKind.Row, Page336 | (4ul << 48)));

        getRow.AllocatedBytes = 100;

        getRow.Allocations = 1;

        getRow.CallActivity = [0, 1, 2];

        scan.EntryFrames = [getRow];

        var publish = tree.AddCall(caller, Frame(0x50, "XeSqlPkg::lock_acquired::Publish+0x0"), 1);

        var builder = new TimeTravelTimeline.Builder();

        builder.Add([Span(0, 0, 100), Span(1, 20, 25), Span(1, 30, 35), Span(2, 40, 41)]);

        var replayed = builder.Build([new(-1, 0, 0x10, 0, 1), new(0, 0, 0x30, Scan, 2), new(0, 0, 0x50, 0, 1)]);

        replayed.MapNodes([caller, getRow, publish]);

        tree.RemoveCallsUnder(node => node.IsExtendedEvents);

        var timeline = replayed.WithoutCallsUnder(node => node.IsExtendedEvents);

        timeline.SetMemory([new TimeTravelAllocation(7, 22, 23, 100, 0xA)], [new TimeTravelFree(7, 31, 32, 0xA)]);

        timeline.SetLifetimes(IteratorLifetimeBuilder.Build(timeline, [scan]));

        var log = new TimeTravelCallLog.Builder([new LocatorRead(0x30, 2, false, LocatorKind.Page)]);

        log.Add(0x30, Scan, Columns(Call(1, 5), Call(2, 6)), 2);

        log.AddLocators([new TimeTravelPointerValue(0x30, Scan, Page336, 1, 2)]);

        var callLog = log.Build();

        callLog.MapNodes([caller, getRow, publish]);

        return new FullTraceResult(tree, callLog, timeline);
    }

    private static CallstackFrame Frame(ulong rva, string symbol, ulong instance = 0) => new()
    {
        Module = "sqlmin",
        Pdb = "sqlmin.pdb",
        Guid = "0123456789ABCDEF0123456789ABCDEF",
        Age = 2,
        Rva = (uint)rva,
        Address = 0x7FF0_0000_0000 + rva,
        Instance = instance,
        Resolved = ResolvedCallstackFrameParser.Parse("sqlmin", symbol)
    };

    private static TimeTravelCallSpan Span(int node, ulong start, ulong end)
        => new(start, 0, end, 0, start, end, node, 7, uint.MaxValue, 1);

    private static ulong[] Call(ulong sequence, ulong rcx)
    {
        var row = new ulong[TimeTravelArgumentCall.ValueCount];

        row[0] = sequence;
        row[2] = rcx;
        row[7] = 1;

        return row;
    }

    private static ulong[] Columns(params ulong[][] rows)
    {
        var columns = new ulong[rows.Length * TimeTravelArgumentCall.ValueCount];

        for (var call = 0; call < rows.Length; call++)
        {
            for (var value = 0; value < TimeTravelArgumentCall.ValueCount; value++)
            {
                columns[value * rows.Length + call] = rows[call][value];
            }
        }

        return columns;
    }
}
