using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Parsing;
using InternalsViewer.Query.Tests.Helpers;
using InternalsViewer.Query.TransactionLog;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Xunit.Abstractions;

namespace InternalsViewer.Query.Tests.IntegrationTests;

public class CallStackTreeIntegrationTests(ITestOutputHelper testOutputHelper)
{
    public ITestOutputHelper TestOutputHelper { get; } = testOutputHelper;

    [Fact]
    public async Task Diagnose_Operators_Survive_Crop()
    {
        var result = await RunQuery("SELECT TOP 10 * FROM dbo.ClusteredTable");

        TestOutputHelper.WriteLine($"Crop window: {result.CropStartUs}..{result.CropEndUs}   events: {result.EngineEvents.Count}");

        foreach (var op in result.EngineEvents.OfType<Query.Events.Operators.ExecutionOperatorEvent>())
        {
            TestOutputHelper.WriteLine($"  Node {op.PlanNodeIdentifier?.NodeId,3} {op.Category,-14} '{op.Name}' "
                + $"Index='{op.IndexName}' TimeUs={op.TimeUs} Dur={op.DurationUs}");
        }
    }

    [Fact]
    public async Task Dump_Call_Stack_Tree_For_Heap()
    {
        await DumpTree("SELECT TOP 100 * FROM dbo.HeapTable");
    }

    [Fact]
    public async Task Dump_Call_Stack_Tree_For_BTree()
    {
        await DumpTree("SELECT TOP 10 * FROM dbo.ClusteredTable");
    }

    [Fact]
    public async Task Diagnose_Scope_For_BTree()
    {
        var result = await RunQuery("SELECT TOP 10 * FROM dbo.ClusteredTable");

        if (result.CallStack is not { } tree)
        {
            TestOutputHelper.WriteLine("No call stack captured (is C:\\Symbols available?)");

            return;
        }

        var events = result.EngineEvents;

        var roots = tree.Root.ChildNodes.ToHashSet();

        TestOutputHelper.WriteLine($"Events: {events.Count}   Roots: {roots.Count}   with CallStack: "
            + $"{events.Count(e => e.CallStack is not null)}");

        // First read group.
        var group = events.OfType<ReadEventGroup>().FirstOrDefault();

        if (group is not null)
        {
            DiagnoseSelection("read group", group.Events, tree, roots);
        }
        else
        {
            TestOutputHelper.WriteLine("No NonCachedReadEventGroup found");
        }

        // First single event that carries a call stack.
        var single = events.FirstOrDefault(e => e is not ReadEventGroup && e.CallStack is not null);

        if (single is not null)
        {
            DiagnoseSelection($"single {single.GetType().Name} '{single.Name}'", [single], tree, roots);
        }

        // How many single events would scope to an EMPTY tree because their whole path is infrastructure.
        var allInfra = events
            .Where(e => e is not ReadEventGroup && e.CallStack is not null)
            .Where(e => AncestorsOf(e.CallStack!).All(n => n.IsInfrastructure))
            .ToList();

        TestOutputHelper.WriteLine("");
        TestOutputHelper.WriteLine($"single events with a call stack whose WHOLE path is infrastructure (would render "
            + $"empty without the reveal-infra fallback): {allInfra.Count}");

        foreach (var e in allInfra.Take(8))
        {
            TestOutputHelper.WriteLine($"  {e.GetType().Name} '{e.Name}' leaf='{e.CallStack!.Symbol}'");
        }
    }

    private static IEnumerable<Callstack.CallStackNode> AncestorsOf(Callstack.CallStackNode leaf)
    {
        for (var node = leaf; node is { IsRoot: false }; node = node.Parent)
        {
            yield return node;
        }
    }

    private void DiagnoseSelection(string label,
                                   IEnumerable<EngineEvent> selected,
                                   Callstack.CallStackTree tree,
                                   HashSet<Callstack.CallStackNode> roots)
    {
        TestOutputHelper.WriteLine("");
        TestOutputHelper.WriteLine($"=== {label} ===");

        var leaves = selected.Where(e => e.CallStack is not null).Select(e => e.CallStack!).Distinct().ToList();

        TestOutputHelper.WriteLine($"leaves with call stack: {leaves.Count}");

        var visible = new HashSet<Callstack.CallStackNode>();

        foreach (var leaf in leaves)
        {
            var pathLen = 0;
            var nonInfra = 0;
            Callstack.CallStackNode? top = null;

            for (var node = leaf; node is { IsRoot: false }; node = node.Parent)
            {
                visible.Add(node);
                pathLen++;

                if (!node.IsInfrastructure)
                {
                    nonInfra++;
                }

                top = node;
            }

            TestOutputHelper.WriteLine($"  leaf '{leaf.Symbol}' pathLen={pathLen} nonInfra={nonInfra} "
                + $"topReachable={(top is not null && roots.Contains(top))} top='{top?.Symbol}'");
        }

        // Count how many nodes BuildVisible would actually show (in-scope AND non-infrastructure).
        var shown = visible.Count(n => !n.IsInfrastructure);

        TestOutputHelper.WriteLine($"visible nodes: {visible.Count}   shown (non-infra, in scope): {shown}");

        // Now simulate the UI's TOP-DOWN BuildVisible from the tree roots and count what it actually yields.
        var topDown = 0;
        var reachable = new HashSet<Callstack.CallStackNode>();

        foreach (var root in tree.Root.ChildNodes)
        {
            topDown += CountVisibleTopDown(root, visible, reachable);
        }

        // How many in-scope non-infra nodes are NOT reached top-down (Parent/Children inconsistency).
        var unreached = visible.Where(n => !n.IsInfrastructure && !reachable.Contains(n)).ToList();

        TestOutputHelper.WriteLine($"top-down shown: {topDown}   in-scope non-infra unreached top-down: {unreached.Count}");

        foreach (var node in unreached.Take(5))
        {
            var parentInChildren = node.Parent is { } p && p.ChildNodes.Contains(node);

            TestOutputHelper.WriteLine($"  UNREACHED '{node.Symbol}' parent='{node.Parent?.Symbol}' "
                + $"parentListsIt={parentInChildren}");
        }

        TestOutputHelper.WriteLine("--- scoped tree as the UI would render it ---");

        foreach (var root in tree.Root.ChildNodes)
        {
            RenderScoped(root, visible, 0);
        }
    }

    // Mirrors the UI's BuildVisible: hide infrastructure and out-of-scope nodes, promoting their visible children up.
    private void RenderScoped(Callstack.CallStackNode node, HashSet<Callstack.CallStackNode> visible, int depth)
    {
        var hidden = node.IsInfrastructure || !visible.Contains(node);

        if (!hidden)
        {
            TestOutputHelper.WriteLine(new string(' ', depth * 2) + node.Symbol);
        }

        foreach (var child in node.ChildNodes.OrderBy(c => c.Order))
        {
            RenderScoped(child, visible, hidden ? depth : depth + 1);
        }
    }

    private static int CountVisibleTopDown(Callstack.CallStackNode node,
                                           HashSet<Callstack.CallStackNode> visible,
                                           HashSet<Callstack.CallStackNode> reachable)
    {
        var count = 0;

        foreach (var child in node.ChildNodes)
        {
            count += CountVisibleTopDown(child, visible, reachable);
        }

        if (!node.IsInfrastructure && visible.Contains(node))
        {
            reachable.Add(node);
            count++;
        }

        return count;
    }

    [Fact]
    public async Task List_CQ_Symbols_For_Mapping()
    {
        var result = await RunQuery("SELECT TOP 10 * FROM dbo.ClusteredTable");

        // The CQScan* iterators live in sqlmin.pdb, the compile classes in sqllang.pdb, etc. — so enumerate every
        // distinct module PDB the stacks touched, using the exact guid/age the engine loaded.
        var pdbs = result.CallStack?
            .Nodes()
            .Select(n => n.Frame)
            .Where(f => f is { Pdb.Length: > 0 })
            .Select(f => (f!.Pdb, f.Guid, f.Age))
            .Distinct()
            .ToList() ?? [];

        if (pdbs.Count == 0)
        {
            TestOutputHelper.WriteLine("No frames captured (is C:\\Symbols available and call stacks on?)");

            return;
        }

        var classesByModule = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var (pdb, guid, age) in pdbs)
        {
            // Same layout the resolver downloads to: <symbols>\<pdb>\<GUID><AGE>\<pdb>.
            var identifier = $"{guid.Replace("-", string.Empty)}{age}".ToUpperInvariant();
            var pdbPath = Path.Combine(@"C:\Symbols", pdb, identifier, pdb);

            if (!File.Exists(pdbPath))
            {
                continue;
            }

            using var resolver = new Callstack.DiaResolver(pdbPath);

            var classes = resolver.EnumerateSymbols("CQScan")
                // The class (the part before ::) is what the operator map keys on; drop RTTI and template noise.
                .Select(s => s.Split("::", 2)[0])
                .Where(s => !s.Contains('`') && !s.Contains('<'))
                .Distinct()
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();

            classesByModule[pdb] = classes;
        }

        var outputPath = Path.Combine(Path.GetTempPath(), "cq-symbols.txt");

        using (var writer = new StreamWriter(outputPath))
        {
            foreach (var (pdb, classes) in classesByModule)
            {
                writer.WriteLine($"# {pdb} ({classes.Count})");

                foreach (var name in classes)
                {
                    writer.WriteLine(name);
                }

                writer.WriteLine();
            }
        }

        TestOutputHelper.WriteLine($"Modules enumerated: {classesByModule.Count}   -> {outputPath}");
        TestOutputHelper.WriteLine("");

        foreach (var (pdb, classes) in classesByModule)
        {
            var scan = classes.Where(c => c.StartsWith("CQ", StringComparison.Ordinal)).ToList();

            TestOutputHelper.WriteLine($"{pdb}: {classes.Count} CQ* classes, {scan.Count} CQScan*");

            if (pdb != "qds.pdb")
            {
                foreach (var name in scan)
                {
                    TestOutputHelper.WriteLine($"    {name}");
                }
            }
        }
    }

    private async Task DumpTree(string sql)
    {
        var result = await RunQuery(sql);

        if (result.CallStack is not { } tree)
        {
            TestOutputHelper.WriteLine("No call stack captured (is C:\\Symbols available?)");

            return;
        }

        TestOutputHelper.WriteLine($"Roots: {tree.Root.ChildNodes.Count()}   Nodes: {tree.Nodes().Count()}");
        TestOutputHelper.WriteLine("");
        TestOutputHelper.WriteLine(tree.Render());
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

        // Callstacks are off by default, so opt in — the tree is only built and resolved when they are captured.
        var eventOptions = new EventOptions { IncludeCallStack = true };

        var result = await executor.TraceQuery(payload, database, eventOptions, @"C:\Symbols", null,
            CancellationToken.None);

        return result;
    }
}
