using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Tests.Documents;
using InternalsViewer.Query;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Waits;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Mcp.Tests;

internal static class TestRuns
{
    public const ulong Scan = 0x9000;

    public const ulong Page336 = 0x0000_0001_0000_0150;

    public static readonly PageAddress Page336Address = new(1, 336);

    public static readonly PageAddress Page337Address = new(1, 337);

    public const string PlanXml = """
        <ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan" Version="1.564" Build="16.0.1000.6">
          <BatchSequence><Batch><Statements>
            <StmtSimple StatementText="SELECT * FROM dbo.Orders" StatementType="SELECT" StatementEstRows="100" StatementSubTreeCost="0.1" QueryHash="0x1" QueryPlanHash="0x2">
              <QueryPlan DegreeOfParallelism="1" CompileTime="2" CachedPlanSize="16">
                <MemoryGrantInfo SerialRequiredMemory="0" SerialDesiredMemory="0" />
                <QueryTimeStats ElapsedTime="5" CpuTime="4" />
                <WaitStats><Wait WaitType="PAGEIOLATCH_SH" WaitTimeMs="1" WaitCount="2" /></WaitStats>
                <RelOp NodeId="0" PhysicalOp="Clustered Index Scan" LogicalOp="Clustered Index Scan" EstimateRows="100" ActualExecutionMode="Row">
                  <RunTimeInformation><RunTimeCountersPerThread Thread="0" ActualRows="120" ActualExecutions="1" ActualLogicalReads="2" /></RunTimeInformation>
                  <IndexScan><Object Schema="[dbo]" Table="[Orders]" Index="[PK_Orders]" /><Predicate><ScalarOperator ScalarString="[dbo].[Orders].[Id]&gt;(1)" /></Predicate></IndexScan>
                </RelOp>
              </QueryPlan>
            </StmtSimple>
          </Statements></Batch></BatchSequence>
        </ShowPlanXML>
        """;

    public static TracePosition At(ulong sequence, ulong steps = 0) => new(sequence, steps);

    public static QueryExportContent Create(bool withTrace = true)
    {
        var scan = new ExecutionOperatorEvent
        {
            Name = "Clustered Index Scan",
            OperatorDescription = "Clustered Index Scan",
            LogicalOperator = "Clustered Index Scan",
            PlanNodeIdentifier = new PlanNodeIdentifier { PlanHandleId = 1, NodeId = 0 },
            ParentNodeId = -1,
            OperatorSchemaName = "dbo",
            OperatorTableName = "Orders",
            OperatorIndexName = "PK_Orders",
            TimeUs = 50,
            DurationUs = 900,
            EmitStartUs = 120,
            RowsProcessed = 120,
            Threads = [new OperatorThread(0, 50, 900, 120)],
            Instances = [Scan]
        };

        var statement = new ExecutionOperatorEvent
        {
            Name = "SELECT",
            OperatorDescription = "SELECT",
            PlanNodeIdentifier = new PlanNodeIdentifier { PlanHandleId = 1, NodeId = -1 },
            NodeLevel = 0,
            TimeUs = 40,
            DurationUs = 960,
            RowsProcessed = 120
        };

        var diskRead = new ReadEventGroup
        {
            Name = "Read",
            Events = [],
            ReadType = ReadType.NonCached,
            Pages = [Page336Address, Page337Address],
            TimeUs = 100,
            DurationUs = 500,
            PlanNodeIdentifier = scan.PlanNodeIdentifier
        };

        var bufferRead = new ReadEventGroup
        {
            Name = "Read",
            Events = [],
            ReadType = ReadType.Cached,
            Pages = [Page336Address],
            TimeUs = 700,
            DurationUs = 10,
            PlanNodeIdentifier = scan.PlanNodeIdentifier
        };

        var wait = new WaitEvent
        {
            Name = "wait_info",
            WaitType = WaitType.LCK_M_S,
            TimeUs = 200,
            DurationUs = 2000,
            ThreadId = 1
        };

        var lockAcquired = new LockEvent
        {
            Name = "lock_acquired",
            LockMode = LockMode.S,
            Resource = new LockResource { ResourceType = LockResourceType.Object, Key = 1, ObjectId = 245575913 },
            TimeUs = 60,
            DurationUs = 800
        };

        var latch = new LatchEvent
        {
            Name = "latch_acquired",
            LatchMode = LatchMode.SH,
            PageAddress = Page336Address,
            TimeUs = 150,
            DurationUs = 5
        };

        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10, "CQueryScan::GetRow+0x0", 0), 1);

        var getRow = tree.AddCall(caller, Frame(0x30, "CQScanRowsetNew::GetRow+0x0", Scan), 2);

        getRow.Locators = new LocatorSet();

        getRow.Locators.Add(new Locator(0, LocatorValueType.Page, Page336));

        getRow.Locators.Add(new Locator(0, LocatorValueType.Row, Page336 | (4ul << 48)));

        getRow.AllocatedBytes = 100;

        getRow.Allocations = 1;

        getRow.Events.Add(diskRead);

        diskRead.CallStack = getRow;

        scan.EntryFrames = [getRow];

        var planNode = new PlanNode
        {
            NodeId = 0,
            PhysicalOperator = "Clustered Index Scan",
            LogicalOperator = "Clustered Index Scan",
            Schema = "dbo",
            Table = "Orders",
            Index = "PK_Orders",
            EstimatedRows = 100,
            RowsOutput = 120,
            IoStats = new PlanIoStatistics { LogicalReads = 2, PhysicalReads = 2 }
        };

        planNode.CountersByThread[0] = new ThreadRuntime(120, 900, ExecutionMode.Row, 0);

        var plan = new ExecutionPlan(1)
        {
            Root = [planNode],
            NodesById = { [0] = planNode },
            Xml = PlanXml
        };

        var result = new QueryResult
        {
            SessionId = "QueryReplay_0123456789abcdef0123456789abcdef",
            IsSuccess = true,
            EngineEvents = [statement, scan, lockAcquired, diskRead, latch, wait, bufferRead],
            ExecutionPlans = [plan],
            CallStackTree = tree,
            RowCount = 120,
            CropStartUs = 10,
            CropEndUs = 1010
        };

        return new QueryExportContent
        {
            ServerName = @".\SQL2025",
            DatabaseName = "Sales",
            SqlText = "SELECT * FROM dbo.Orders",
            Messages = ["(120 rows affected)"],
            Result = result,
            Extras = withTrace ? [FullTrace(tree, caller, getRow, scan)] : []
        };
    }

    public static (McpDocumentResolver Resolver, TestDocument Document) CreateResolver(QueryExportContent run)
    {
        var document = new TestDocument { Run = run };

        return (new McpDocumentResolver(new TestDocumentSource(document)), document);
    }

    private static TraceResult FullTrace(CallStackTree tree, CallStackNode caller, CallStackNode getRow, ExecutionOperatorEvent scan)
    {
        getRow.CallActivity = [0, 1, 2];

        var publish = tree.AddCall(caller, Frame(0x50, "XeSqlPkg::lock_acquired::Publish+0x0"), 1);

        var builder = new TraceTimeline.Builder();

        builder.Add([Span(0, 0, 100), Span(1, 20, 25), Span(1, 30, 35), Span(2, 40, 41)]);

        var timeline = builder.Build([new(-1, 0, 0x10, 0, 1), new(0, 0, 0x30, Scan, 2), new(0, 0, 0x50, 0, 1)]);

        timeline.MapNodes([caller, getRow, publish]);

        tree.RemoveCallsUnder(node => node.IsExtendedEvents);

        timeline.RemoveCallsUnder(node => node.IsExtendedEvents);

        var memory = MemoryIndex.Build([new MemoryAllocation(7, At(22), At(23), 100, 0xA)], [new MemoryFree(7, At(31), At(32), 0xA)]);

        var lifetimes = IteratorLifetimeBuilder.Build(timeline, memory, [scan], [], []);

        var storeBuilder = new TraceCallStore.Builder();

        storeBuilder.Add(0x30, Scan, TraceCallRows.Chunk(Call(1, 5), Call(2, 6)), 2);

        storeBuilder.AddLocator(0x30, Scan, 1, new Locator(2, LocatorValueType.Page, Page336), false);

        var callStore = storeBuilder.Build();

        callStore.MapNodes([caller, getRow, publish]);

        return new TraceResult(tree, callStore, timeline) { Memory = memory, Lifetimes = lifetimes };
    }

    private static CallstackFrame Frame(ulong rva, string symbol, ulong instance = 0) => new()
    {
        Module = "sqlmin",
        Pdb = "sqlmin.pdb",
        Guid = "0123456789ABCDEF0123456789ABCDEF",
        Age = 2,
        Rva = (uint)rva,
        Address = 0x7FF0_0000_0000 + rva,
        InstanceAddress = instance,
        Resolved = ResolvedCallstackFrameParser.Parse("sqlmin", symbol)
    };

    private static CallSpan Span(int callNodeIndex, ulong start, ulong end)
        => new(start, 0, end, 0, start, end, callNodeIndex, 7, uint.MaxValue, 1);

    private static ulong[] Call(ulong sequence, ulong rcx) => TraceCallRows.Row(sequence, rcx: rcx, callNodeIndex: 1);
}
