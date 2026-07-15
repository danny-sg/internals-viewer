using InternalsViewer.Internals;
using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Parsing;
using InternalsViewer.Query.Parsing.Plans;
using InternalsViewer.Query.Tests.Helpers;
using InternalsViewer.Query.TransactionLog;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace InternalsViewer.Query.Tests.IntegrationTests;

/// <summary>
/// Runs real queries and dumps how each plan operator's call-stack segment came out
/// </summary>
/// <remarks>
/// Diagnostics, not assertions — they need a live server and C:\Symbols, and what they print is the thing that is
/// otherwise only visible by eye in the UI: which frame each operator was entered through, whether that came from the
/// mapping file or from event ownership, and what the segment then contains. Every wrong guess about this feature so far
/// came from inferring the plan backwards from a pasted stack; this reads it from the plan instead.
/// </remarks>
public class OperatorScopeIntegrationTests(ITestOutputHelper testOutputHelper)
{
    private ITestOutputHelper Output { get; } = testOutputHelper;

    private const string HeapSeek = "SELECT * FROM dbo.HeapTable WHERE NumberField = 500";

    private const string ClusteredSeek = "SELECT * FROM dbo.ClusteredTable WHERE TextField = 'This is row 54321'";

    private const string HeapCount = "SELECT COUNT(*) FROM dbo.HeapTable";

    private const string HashJoin = """
                                    SELECT c.Id
                                          ,c.TextField
                                    FROM   dbo.HeapTable h
                                           INNER HASH JOIN dbo.ClusteredTable c
                                             ON c.Id = h.NumberField
                                    """;

    /// <summary>
    /// A hash join's build and probe run under Open/Iterate, not GetRow — so its frames sit somewhere else entirely
    /// </summary>
    [Fact]
    public async Task Dump_Hash_Join_Frames()
    {
        var result = await RunQuery(HashJoin, await LoadDatabase());

        if (result.CallStackTree is not { } tree)
        {
            Output.WriteLine("No call stack captured (is C:\\Symbols available?)");

            return;
        }

        var operators = result.EngineEvents.OfType<ExecutionOperatorEvent>()
                              .Where(o => o.PlanNodeIdentifier is not null)
                              .OrderBy(o => o.PlanNodeIdentifier!.NodeId)
                              .ToList();

        foreach (var op in operators)
        {
            Output.WriteLine($"node {op.PlanNodeIdentifier!.NodeId,3} parent {op.ParentNodeId?.ToString() ?? "-",-3} "
                             + $"'{op.Name}' target='{op.TargetLabel}'");

            Output.WriteLine($"       entry: {Describe(op.EntryFrames)}");
            Output.WriteLine($"       exit : {Describe(op.ExitFrames)}");
        }

        // Reachability: an entry frame the tree cannot reach from its root is one the UI can never render, however
        // correctly it was resolved.
        var reachable = tree.Nodes().ToHashSet();

        Output.WriteLine("");
        Output.WriteLine($"--- tree nodes={reachable.Count} ---");

        foreach (var op in operators)
        {
            foreach (var entry in op.EntryFrames)
            {
                var root = entry.Ancestors().Last();

                Output.WriteLine($"  node {op.PlanNodeIdentifier!.NodeId} entry '{entry.Symbol}' "
                                 + $"reachable={reachable.Contains(entry)} rootOfPath='{root.Symbol}' "
                                 + $"rootIsTreeChild={tree.Root.ChildNodes.Contains(root)}");
            }
        }

        // Any path an entry frame sits on whose root is not one of the tree's own children: a stack the tree cannot
        // reach, so the UI can never render it however correctly it resolved.
        var orphans = operators.SelectMany(o => o.EntryFrames)
                               .Where(entry => !reachable.Contains(entry))
                               .Select(entry => entry.Ancestors().Last())
                               .Distinct()
                               .ToList();

        Output.WriteLine("");
        Output.WriteLine($"--- orphaned roots: {orphans.Count} ---");

        foreach (var orphan in orphans)
        {
            // Whether it was never attached, or was attached and then cut loose: if its parent is the tree root but the
            // root no longer lists it, the graft removed it and left this subtree hanging off the removed node.
            var parent = orphan.Parent;

            Output.WriteLine($"  '{orphan.Symbol}' parent={(parent is null ? "null" : parent.IsRoot ? "ROOT" : parent.Symbol)}"
                             + $" rootListsIt={parent is not null && parent.Children.ContainsValue(orphan)}"
                             + $" twinInTree={reachable.Any(n => n.Key == orphan.Key)}");
        }

        Output.WriteLine("");
        Output.WriteLine("--- every CQScanHash frame in the tree, and where it sits ---");

        foreach (var frame in tree.Nodes().Where(n => n.Symbol.Contains("CQScanHash")))
        {
            Output.WriteLine($"  {frame.Symbol,-40} boundary={frame.IsOperatorBoundary,-5} events={frame.Events.Count}");
            Output.WriteLine($"      path: {string.Join(" < ", frame.Ancestors().Take(6).Select(f => f.Symbol))}");
        }
    }

    [Theory]
    [InlineData(HeapSeek)]
    [InlineData(ClusteredSeek)]
    [InlineData(HeapCount)]
    public async Task Dump_Operator_Scopes(string sql)
    {
        var result = await RunQuery(sql, await LoadDatabase());

        if (result.CallStackTree is not { } tree)
        {
            Output.WriteLine("No call stack captured (is C:\\Symbols available?)");

            return;
        }

        var operators = result.EngineEvents.OfType<ExecutionOperatorEvent>()
                              .Where(o => o.PlanNodeIdentifier is not null)
                              .OrderBy(o => o.PlanNodeIdentifier!.NodeId)
                              .ToList();

        Output.WriteLine($"=== {sql}");
        Output.WriteLine($"events={result.EngineEvents.Count} operators={operators.Count} treeNodes={tree.Nodes().Count()}");
        Output.WriteLine("");

        foreach (var op in operators)
        {
            var id = op.PlanNodeIdentifier!;

            var own = result.EngineEvents.Count(e => e is not ExecutionOperatorEvent && e.PlanNodeIdentifier == id);

            Output.WriteLine($"node {id.NodeId,3}  parent {op.ParentNodeId?.ToString() ?? "-",-3}  '{op.Name}'"
                             + $"  target='{op.TargetLabel}'  ownEvents={own}");

            Output.WriteLine($"        entry: {Describe(op.EntryFrames)}");
            Output.WriteLine($"        exit : {Describe(op.ExitFrames)}");
        }

        Output.WriteLine("");
        Output.WriteLine("--- segments ---");

        foreach (var op in operators)
        {
            DumpSegment(tree, result.EngineEvents, operators, op);
        }
    }

    /// <summary>
    /// Which events the matcher could see against each node, and which frames its entry could have come from
    /// </summary>
    /// <remarks>
    /// The two inputs to entry detection, side by side: whether the mapping names a frame on the node's stacks, and
    /// whether ownership can separate the node from its siblings. An operator with neither is one the UI shows as
    /// "full path".
    /// </remarks>
    [Theory]
    [InlineData(HeapSeek)]
    [InlineData(ClusteredSeek)]
    [InlineData(HeapCount)]
    public async Task Dump_Entry_Frame_Sources(string sql)
    {
        var result = await RunQuery(sql, await LoadDatabase());

        if (result.CallStackTree is null)
        {
            Output.WriteLine("No call stack captured (is C:\\Symbols available?)");

            return;
        }

        var operators = result.EngineEvents.OfType<ExecutionOperatorEvent>()
                              .Where(o => o.PlanNodeIdentifier is not null)
                              .OrderBy(o => o.PlanNodeIdentifier!.NodeId)
                              .ToList();

        Output.WriteLine($"=== {sql}");

        foreach (var op in operators)
        {
            var subtree = Subtree(operators, op);

            var leaves = Leaves(result.EngineEvents, subtree);

            var named = leaves.SelectMany(leaf => leaf.Ancestors())
                              .Where(frame => frame.IsEntryFrameFor(op.Name))
                              .Select(frame => frame.Symbol)
                              .Distinct()
                              .ToList();

            Output.WriteLine($"node {op.PlanNodeIdentifier!.NodeId,3} '{op.Name,-22}' subtreeLeaves={leaves.Count,-4} "
                             + $"mappingNames=[{string.Join(", ", named)}]  resolved=[{Describe(op.EntryFrames)}]");
        }
    }

    /// <summary>
    /// What EventPlanNodeMatcher attributed to each operator, so a compile-phase event sitting in one is visible
    /// </summary>
    [Theory]
    [InlineData(HeapSeek)]
    [InlineData(ClusteredSeek)]
    [InlineData(HeapCount)]
    public async Task Dump_Matched_Events_Per_Node(string sql)
    {
        var result = await RunQuery(sql, await LoadDatabase());

        Output.WriteLine($"=== {sql}");

        foreach (var group in result.EngineEvents
                                    .Where(e => e is not ExecutionOperatorEvent && e.PlanNodeIdentifier is not null)
                                    .GroupBy(e => e.PlanNodeIdentifier!.NodeId)
                                    .OrderBy(g => g.Key))
        {
            Output.WriteLine($"node {group.Key}:");

            // Expanded, because a lock reaches the node as a group and the mode — the thing that says whether it is a
            // data lock or a compiler's schema lock — is on its members.
            foreach (var kind in group.SelectMany(e => e.SelfAndOwned()).GroupBy(Kind).OrderByDescending(k => k.Count()))
            {
                var leaf = kind.FirstOrDefault(e => e.CallStack is not null)?.CallStack;

                Output.WriteLine($"    {kind.Key,-46} count={kind.Count(),-4} leaf='{leaf?.Symbol}'"
                                 + $" via='{leaf?.Parent?.Parent?.Symbol}'");
            }
        }
    }

    private static string Kind(EngineEvent engineEvent) => engineEvent switch
    {
        Events.Locks.LockEvent l => $"Lock {l.Resource.ResourceType}/{l.LockMode} '{l.Name}'",
        Events.Locks.LockGroup g => $"LockGroup ({g.Events.Count})",
        _ => $"{engineEvent.GetType().Name} '{engineEvent.Name}'",
    };

    /// <summary>
    /// Every distinct iterator frame the query actually executed, with the plan nodes seen beneath it
    /// </summary>
    /// <remarks>
    /// The mapping file's raw material. A frame whose nodes are exactly one operator's subtree is one ownership can
    /// place without a name; the rest are what the file has to cover.
    /// </remarks>
    [Theory]
    [InlineData(HeapSeek)]
    [InlineData(ClusteredSeek)]
    [InlineData(HeapCount)]
    public async Task Dump_Iterator_Frames(string sql)
    {
        var result = await RunQuery(sql, await LoadDatabase());

        if (result.CallStackTree is not { } tree)
        {
            Output.WriteLine("No call stack captured (is C:\\Symbols available?)");

            return;
        }

        Output.WriteLine($"=== {sql}");

        // The node id is on the top-level event, but the frames are on what it owns — so index from the top down rather
        // than reading PlanNodeIdentifier off the leaf events, which are a group's members and carry none.
        var nodesByFrame = new Dictionary<CallStackNode, SortedSet<int>>();

        foreach (var engineEvent in result.EngineEvents)
        {
            if (engineEvent is ExecutionOperatorEvent || engineEvent.PlanNodeIdentifier is not { } id)
            {
                continue;
            }

            foreach (var leaf in engineEvent.SelfAndOwned().Select(e => e.CallStack).OfType<CallStackNode>())
            {
                foreach (var frame in leaf.Ancestors())
                {
                    if (!nodesByFrame.TryGetValue(frame, out var nodes))
                    {
                        nodes = [];

                        nodesByFrame[frame] = nodes;
                    }

                    nodes.Add(id.NodeId);
                }
            }
        }

        foreach (var frame in tree.Nodes().Where(n => n.Symbol.Contains("CQScan") || n.Symbol.Contains("CQueryScan")))
        {
            var nodes = nodesByFrame.GetValueOrDefault(frame) ?? [];

            var caller = frame.Parent is { Frame: not null } parent ? nodesByFrame.GetValueOrDefault(parent) : null;

            Output.WriteLine($"  {frame.Symbol,-44} op='{frame.Operator,-16}' boundary={frame.IsOperatorBoundary,-5} "
                             + $"nodes=[{string.Join(",", nodes)}] caller=[{string.Join(",", caller ?? [])}]");
        }
    }

    private void DumpSegment(CallStackTree tree,
                             IReadOnlyList<EngineEvent> events,
                             List<ExecutionOperatorEvent> operators,
                             ExecutionOperatorEvent op)
    {
        var scope = Scope(events, operators, op);

        Output.WriteLine("");
        Output.WriteLine($"### node {op.PlanNodeIdentifier!.NodeId} '{op.Name}' "
                         + $"{(op.EntryFrames.Count == 0 ? "(UNSEGMENTED - full path)" : string.Empty)}");

        if (scope.Count == 0)
        {
            Output.WriteLine("   (no events in subtree)");

            return;
        }

        var projected = tree.Project(include: scope.Contains,
                                     cutAt: op.EntryFrames.Count > 0 ? op.EntryFrames.Contains : null,
                                     stopBelow: op.ExitFrames.Count > 0 ? op.ExitFrames.Contains : null);

        Output.WriteLine(Trim(projected.Render()));
    }

    // The rendered segment minus the XEvent publishing tail, which is the same four frames under every event site.
    private static string Trim(string render)
    {
        var kept = render.Split('\n')
                         .Where(line => !line.Contains("::Publish") && !line.Contains("GenericEvent::"))
                         .ToList();

        return string.Join('\n', kept);
    }

    private static string Describe(IReadOnlyList<CallStackNode> frames)
        => frames.Count == 0 ? "(none)" : string.Join(", ", frames.Select(f => f.Symbol));

    private static HashSet<PlanNodeIdentifier> Subtree(List<ExecutionOperatorEvent> operators, ExecutionOperatorEvent op)
    {
        var subtree = new HashSet<PlanNodeIdentifier> { op.PlanNodeIdentifier! };

        var added = true;

        while (added)
        {
            added = false;

            foreach (var candidate in operators)
            {
                if (candidate.ParentNodeId is { } parent
                    && subtree.Contains(new PlanNodeIdentifier { PlanHandleId = candidate.PlanNodeIdentifier!.PlanHandleId, NodeId = parent })
                    && subtree.Add(candidate.PlanNodeIdentifier))
                {
                    added = true;
                }
            }
        }

        return subtree;
    }

    // What the view scopes an operator to: its plan subtree's events, expanded through groups and folded ends.
    private static HashSet<EngineEvent> Scope(IReadOnlyList<EngineEvent> events,
                                              List<ExecutionOperatorEvent> operators,
                                              ExecutionOperatorEvent op)
    {
        var subtree = Subtree(operators, op);

        return events.Where(e => e is not ExecutionOperatorEvent
                                 && e.PlanNodeIdentifier is { } id
                                 && subtree.Contains(id))
                     .ExpandOwned();
    }

    private static List<CallStackNode> Leaves(IReadOnlyList<EngineEvent> events, HashSet<PlanNodeIdentifier> subtree)
        => events.Where(e => e is not ExecutionOperatorEvent && e.PlanNodeIdentifier is { } id && subtree.Contains(id))
                 .SelectMany(e => e.SelfAndOwned())
                 .Select(e => e.CallStack)
                 .OfType<CallStackNode>()
                 .Distinct()
                 .ToList();


    private async Task<QueryResult> RunQuery(string sql, DatabaseSource database)
    {
        var logger = TestLogger.GetLogger<QueryRunner>(Output, LogLevel.Warning);

        var eventReader = new EventReader(TestLogger.GetLogger<EventReader>(Output, LogLevel.Warning));

        var logReader = new LogRecordReader(TestLogger.GetLogger<LogRecordReader>(Output, LogLevel.Warning));

        var executor = new QueryRunner(logger, eventReader, logReader);

        var payload = new ExecuteSqlPayload(sql, new QueryOptions(), StatementType.Select, null);

        return await executor.TraceQuery(payload, database, new EventOptions { IncludeCallStack = true },
                                         @"C:\Symbols", null, CancellationToken.None);
    }

    private async Task<DatabaseSource> LoadDatabase()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("Local");

        using var host = Host.CreateDefaultBuilder()
                             .ConfigureServices((_, services) => services.RegisterServices())
                             .Build();

        var databaseService = host.Services.GetRequiredService<IDatabaseService>();

        var connection = new ServerConnectionFactory(TestLogger.GetLogger<QueryPageReader>(Output))
            .Create(c => c.ConnectionString = connectionString);

        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        return await databaseService.LoadAsync(databaseName, connection, CancellationToken.None);
    }
}
