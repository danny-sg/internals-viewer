using System.IO.Compression;
using System.Text.Json.Nodes;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.CallStack;
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
public class QueryExportTests
{
    private const ulong Scan = 0x9000;

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

    private static async Task<byte[]> Export(QueryExportContent content)
    {
        using var stream = new MemoryStream();

        await new QueryExport([]).WriteAsync(stream, content, CancellationToken.None);

        return stream.ToArray();
    }

    private static async Task<QueryExportContent> Import(byte[] export)
    {
        using var stream = new MemoryStream(export);

        return await new QueryExport([]).ReadAsync(stream, id => id == Orders.AllocationUnitId ? Orders : null, CancellationToken.None);
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

            entries[entry.FullName] = entry.FullName == QueryExport.ManifestEntry ? WithoutExportTime(copy.ToArray()) : copy.ToArray();
        }

        return entries;
    }

    private static byte[] WithoutExportTime(byte[] manifest)
    {
        var json = JsonNode.Parse(manifest)!.AsObject();

        json.Remove("ExportedAt");

        return System.Text.Encoding.UTF8.GetBytes(json.ToJsonString());
    }

    private static QueryExportContent Content()
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
            Instances = [Scan]
        };

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

        return new QueryExportContent
        {
            ServerName = ".\\SQL2025",
            DatabaseName = "Sales",
            SqlText = "SELECT * FROM dbo.Orders",
            Messages = ["Running Query - Completed in 00:00:01"],
            Result = result
        };
    }

    private static CallstackFrame Frame(ulong rva, string symbol) => new()
    {
        Module = "sqlmin",
        Pdb = "sqlmin.pdb",
        Guid = "0123456789ABCDEF0123456789ABCDEF",
        Age = 2,
        Rva = (uint)rva,
        Address = 0x7FF0_0000_0000 + rva,
        Resolved = ResolvedCallstackFrameParser.Parse("sqlmin", symbol)
    };
}
