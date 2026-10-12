using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Iterators;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class IteratorPlanLinkerTests
{
    private const ulong ReturnedFlag = 1ul << 32;

    [Fact]
    public void Each_Plan_Object_Takes_The_Plan_Node_In_The_Same_Place()
    {
        var recording = new Recording();

        var hash = recording.Factory(recording.Root, "CXteHash", sequence: 10, planObject: 0xA0, returned: 0xA9);

        var hashIterator = recording.Constructor(hash, "CQScanHash", sequence: 11, instance: 0xA1, planObject: 0xA0, "Hash Match");

        var scan = recording.Factory(hashIterator, "CXteTableScan", sequence: 12, planObject: 0xB0, returned: 0xB9);

        recording.Constructor(scan, "CQScanTableScanNew", sequence: 13, instance: 0xB1, planObject: 0xB0, "Table Scan,*Index Scan");

        var range = recording.Factory(hashIterator, "CXteRange", sequence: 14, planObject: 0xC0, returned: 0xC9);

        recording.Constructor(range, "CQScanRangeNew", sequence: 15, instance: 0xC1, planObject: 0xC0, "*Index Seek");

        var link = recording.Link(Operator(0, "Hash Match"), Operator(1, "Clustered Index Scan", 0), Operator(2, "Index Seek", 0));

        Assert.Equal([(0xA0ul, 0), (0xB0ul, 1), (0xC0ul, 2)], NodesOf(link.PlanObjects));
        Assert.Equal([(0xA1ul, 0), (0xA9ul, 0), (0xB1ul, 1), (0xB9ul, 1), (0xC1ul, 2), (0xC9ul, 2)], NodesOf(link.Instances));
    }

    [Fact]
    public void Plan_Objects_Merged_Into_One_Call_Path_Keep_Their_Own_Plan_Nodes()
    {
        var recording = new Recording();

        var hash = recording.Factory(recording.Root, "CXteHash", sequence: 10, planObject: 0xA0);

        var hashIterator = recording.Constructor(hash, "CQScanHash", sequence: 11, instance: 0xA1, planObject: 0xA0, "Hash Match");

        var first = recording.Factory(hashIterator, "CXteExchange", sequence: 20, planObject: 0xD0);

        var firstIterator = recording.Constructor(first, "CQScanExchangeNew", 21, instance: 0xD1, planObject: 0xD0, "Parallelism");

        var firstScan = recording.Factory(firstIterator, "CXteRange", sequence: 22, planObject: 0xD5);

        recording.Constructor(firstScan, "CQScanRangeNew", sequence: 23, instance: 0xD6, planObject: 0xD5, "*Scan");

        var second = recording.Factory(hashIterator, "CXteExchange", sequence: 30, planObject: 0xE0);

        var secondIterator = recording.Constructor(second, "CQScanExchangeNew", 31, instance: 0xE1, planObject: 0xE0, "Parallelism");

        var project = recording.Factory(secondIterator, "CXteProject", sequence: 32, planObject: 0xE5, returned: 0xE9);

        var secondScan = recording.Factory(project, "CXteRange", sequence: 33, planObject: 0xE7, returned: 0xE9);

        recording.Constructor(secondScan, "CQScanRangeNew", sequence: 34, instance: 0xE8, planObject: 0xE7, "*Scan");

        Assert.Same(first, second);

        var link = recording.Link(Operator(0, "Hash Match"),
                                  Operator(1, "Parallelism", 0),
                                  Operator(2, "Index Scan", 1),
                                  Operator(3, "Parallelism", 0),
                                  Operator(4, "Compute Scalar", 3),
                                  Operator(5, "Index Scan", 4));

        Assert.Equal([(0xA0ul, 0), (0xD0ul, 1), (0xD5ul, 2), (0xE0ul, 3), (0xE5ul, 4), (0xE7ul, 5)], NodesOf(link.PlanObjects));
        Assert.Equal(5, link.NodeOf(0xE9)?.NodeId);
    }

    [Fact]
    public void An_Iterator_Built_On_Another_Thread_Links_Through_Its_Constructor()
    {
        var recording = new Recording();

        var exchange = recording.Factory(recording.Root, "CXteExchange", sequence: 10, planObject: 0xA0);

        var exchangeIterator = recording.Constructor(exchange, "CQScanExchangeNew", 11, instance: 0xA1, planObject: 0xA0, "Parallelism");

        var scan = recording.Factory(exchangeIterator, "CXteTableScan", sequence: 12, planObject: 0xB0);

        recording.Constructor(scan, "CQScanTableScanNew", sequence: 13, instance: 0xB1, planObject: 0xB0, "Table Scan");

        var worker = recording.Frame(recording.Root, "CXTransport", "CreateProducerPart");

        recording.Constructor(worker, "CQScanXProducerNew", sequence: 40, instance: 0xF1, planObject: 0xA0, "Parallelism", thread: 2);

        recording.Constructor(worker, "CQScanTableScanNew", sequence: 41, instance: 0xF2, planObject: 0xB0, "Table Scan", thread: 2);

        var link = recording.Link(Operator(0, "Parallelism"), Operator(1, "Table Scan", 0));

        Assert.Equal(0, link.NodeOf(0xF1)?.NodeId);
        Assert.Equal(1, link.NodeOf(0xF2)?.NodeId);
    }

    [Fact]
    public void A_Profile_Wrapper_Takes_The_Plan_Node_Of_The_Iterator_It_Calls()
    {
        var recording = new Recording();

        var scan = recording.Factory(recording.Root, "CXteTableScan", sequence: 10, planObject: 0xB0);

        recording.Constructor(scan, "CQScanTableScanNew", sequence: 11, instance: 0xB1, planObject: 0xB0, "Table Scan");

        var query = recording.Instance(recording.Root, "CQueryScan", "GetRow", 0x90, SymbolCategory.QueryOperator, "Iterator");

        var wrapper = recording.Instance(query, "CQScanProfileNew", "GetRow", 0x95, SymbolCategory.Tracing, "Profiling");

        recording.Instance(wrapper, "CQScanNew", "GetRowOrReQualifyHelper", 0xB1, SymbolCategory.QueryOperator, iterator: null);

        var link = recording.Link(Operator(0, "Table Scan"));

        Assert.Equal(0, link.NodeOf(0x95)?.NodeId);
        Assert.Null(link.NodeOf(0x90));
    }

    [Fact]
    public void Batch_Iterators_Take_The_Plan_Node_Of_The_Plan_Object_They_Were_Built_For()
    {
        var recording = new Recording();

        var hash = recording.Factory(recording.Root, "CXteHash", sequence: 10, planObject: 0xA0);

        recording.Constructor(hash, "CQScanBatchHelper", sequence: 11, instance: 0xA1, planObject: 0xA0, "Batch Mode Adapter");

        var setup = recording.Frame(recording.Root, "CQueryScan", "Setup");

        var hashBatch = recording.BatchFactory(setup, sequence: 20, planObject: 0xA0);

        recording.BatchIterator(hashBatch, "CBpXteHashAgg", sequence: 21, batchObject: 0xAB, returned: 0xA5);

        var scanBatch = recording.BatchFactory(hashBatch, sequence: 22, planObject: 0xB0);

        recording.BatchIterator(scanBatch, "CBpXteColumnStoreScan", sequence: 23, batchObject: 0xBB, returned: 0xB5);

        var query = recording.Instance(recording.Root, "CQueryScan", "GetRow", 0x90, SymbolCategory.QueryOperator, "Iterator");

        var aggregate = recording.Instance(query,
                                           "CBpQScanHashAggNew",
                                           "BpOpen",
                                           0xA5,
                                           SymbolCategory.QueryOperator,
                                           "Hash Match",
                                           "Hash Match");

        recording.Instance(aggregate,
                           "CBpQScanColumnStoreScan",
                           "BpOpen",
                           0xB5,
                           SymbolCategory.ColumnStore,
                           "Scan",
                           "*Columnstore Index Scan");

        var link = recording.Link(Operator(0, "Hash Match"), Operator(1, "Columnstore Index Scan", 0));

        Assert.Equal([(0xA0ul, 0), (0xABul, 0), (0xB0ul, 1), (0xBBul, 1)], NodesOf(link.PlanObjects));
        Assert.Equal([(0xA1ul, 0), (0xA5ul, 0), (0xB5ul, 1)], NodesOf(link.Instances));
    }

    [Fact]
    public void A_Trace_Without_Factory_Calls_Links_Nothing()
    {
        var recording = new Recording();

        recording.Constructor(recording.Root, "CQScanTableScanNew", sequence: 11, instance: 0xB1, planObject: 0xB0, "Table Scan");

        Assert.Same(IteratorPlanLink.Empty, recording.Link(Operator(0, "Table Scan")));
    }

    private static List<(ulong Address, int NodeId)> NodesOf(IReadOnlyDictionary<ulong, PlanNodeIdentifier> links)
        => [.. links.Select(l => (l.Key, l.Value.NodeId)).OrderBy(l => l.Key)];

    private static ExecutionOperatorEvent Operator(int node, string physicalOperator, int? parent = null) => new()
    {
        Name = physicalOperator,
        OperatorDescription = physicalOperator,
        PlanNodeIdentifier = new PlanNodeIdentifier { PlanHandleId = 1, NodeId = node },
        ParentNodeId = parent,
    };

    private sealed class Recording
    {
        private const ulong ModuleBase = 0x7FF0_0000_0000;

        public CallStackNode Root => Tree.Root;

        private CallStackTree Tree { get; } = new();

        private List<CallStackNode?> Nodes { get; } = [];

        private Dictionary<CallStackNode, int> Indexes { get; } = new(ReferenceEqualityComparer.Instance);

        private Dictionary<ulong, List<ulong[]>> Rows { get; } = new();

        public CallStackNode Factory(CallStackNode parent, string className, ulong sequence, ulong planObject, ulong returned = 0)
            => Call(parent, FrameOf(className, "QScanGet", SymbolCategory.QueryOperator), sequence, 1, planObject, 0, 0, returned);

        public CallStackNode BatchFactory(CallStackNode parent, ulong sequence, ulong planObject)
            => Call(parent, FrameOf("CBpXte", "CreateBatchIterators", SymbolCategory.QueryOperator), sequence, 1, 0, planObject, 0, 0);

        public CallStackNode BatchIterator(CallStackNode parent, string className, ulong sequence, ulong batchObject, ulong returned)
            => Call(parent, FrameOf(className, "PbpqsGet", SymbolCategory.QueryOperator), sequence, 1, batchObject, 0, 0, returned);

        public CallStackNode Constructor(CallStackNode parent,
                                         string className,
                                         ulong sequence,
                                         ulong instance,
                                         ulong planObject,
                                         string planOperator,
                                         uint thread = 1)
            => Call(parent,
                    FrameOf(className, className, SymbolCategory.QueryOperator, planOperator),
                    sequence,
                    thread,
                    instance,
                    0,
                    planObject,
                    instance);

        public CallStackNode Frame(CallStackNode parent, string className, string method)
            => Add(parent, FrameOf(className, method, SymbolCategory.Unknown));

        public CallStackNode Instance(CallStackNode parent,
                                      string className,
                                      string method,
                                      ulong instance,
                                      SymbolCategory category,
                                      string? iterator,
                                      string? planOperator = null)
            => Add(parent, FrameOf(className, method, category, planOperator, iterator) with { Instance = instance });

        public IteratorPlanLink Link(params ExecutionOperatorEvent[] operators)
        {
            var builder = new TimeTravelCallLog.Builder();

            foreach (var (address, rows) in Rows)
            {
                builder.Add(address, 0, Chunk(rows), rows.Count);
            }

            return IteratorPlanLinker.Link(builder.Build(), Nodes, operators);
        }

        private CallStackNode Call(CallStackNode parent,
                                   CallstackFrame frame,
                                   ulong sequence,
                                   uint thread,
                                   ulong rcx,
                                   ulong rdx,
                                   ulong r8,
                                   ulong returned)
        {
            var node = Add(parent, frame);

            var flags = thread | (returned != 0 ? ReturnedFlag : 0);

            if (!Rows.TryGetValue(frame.Address, out var rows))
            {
                rows = [];

                Rows[frame.Address] = rows;
            }

            rows.Add([sequence, flags, rcx, rdx, r8, 0, returned, (ulong)Indexes[node]]);

            return node;
        }

        private CallStackNode Add(CallStackNode parent, CallstackFrame frame)
        {
            var node = Tree.AddCall(parent, frame, 1);

            if (!Indexes.ContainsKey(node))
            {
                Indexes[node] = Nodes.Count;

                Nodes.Add(node);
            }

            return node;
        }

        private static CallstackFrame FrameOf(string className,
                                              string method,
                                              SymbolCategory category,
                                              string? planOperator = null,
                                              string? iterator = null)
        {
            var rva = (uint)StringComparer.Ordinal.GetHashCode($"{className}::{method}") & 0x00FF_FFFF;

            return new CallstackFrame
            {
                Module = "sqlmin",
                Rva = rva,
                Address = ModuleBase + rva,
                Resolved = new ResolvedCallstackFrame
                {
                    ClassName = className,
                    MethodName = method,
                    SymbolCategory = category,
                    Iterator = iterator,
                    PlanOperator = planOperator is null ? [] : [.. planOperator.Split(',').Select(GlobPattern.Parse)]
                }
            };
        }

        private static ulong[] Chunk(List<ulong[]> rows)
        {
            var columns = new ulong[rows.Count * TimeTravelArgumentCall.ValueCount];

            for (var call = 0; call < rows.Count; call++)
            {
                for (var value = 0; value < TimeTravelArgumentCall.ValueCount; value++)
                {
                    columns[value * rows.Count + call] = rows[call][value];
                }
            }

            return columns;
        }
    }
}
