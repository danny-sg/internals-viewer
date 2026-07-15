using InternalsViewer.Internals;
using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Memory;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Parsing;
using InternalsViewer.Query.Tests.Helpers;
using InternalsViewer.Query.TransactionLog;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace InternalsViewer.Query.Tests.IntegrationTests;

public class CallStackTreeIntegrationTests(ITestOutputHelper testOutputHelper)
{
    public ITestOutputHelper TestOutputHelper { get; } = testOutputHelper;

    [Fact]
    public async Task Diagnose_Crop_Start()
    {
        var result = await RunQuery(
            """
            SELECT TOP 1000 *
            FROM dbo.LockEscalationDemo WITH (UPDLOCK)
            WHERE Category = 1;
            """);

        TestOutputHelper.WriteLine($"cropStart={result.CropStartUs}  cropEnd={result.CropEndUs}");

        // The earliest kept events — whatever sits near TimeUs 0 is what pulls cropStart back before the query.
        foreach (var e in result.EngineEvents.OrderBy(e => e.TimeUs).Take(18))
        {
            TestOutputHelper.WriteLine($"  TimeUs={e.TimeUs,-8} dur={e.DurationUs,-9} {e.GetType().Name,-26} "
                + $"'{e.Name}' planHandle={e.PlanHandleId}");
        }
    }

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

        if (result.CallStackTree is not { } tree)
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

    private static IEnumerable<CallStackNode> AncestorsOf(CallStackNode leaf)
    {
        for (var node = leaf; node is { IsRoot: false }; node = node.Parent)
        {
            yield return node;
        }
    }

    private void DiagnoseSelection(string label,
                                   IEnumerable<EngineEvent> selected,
                                   CallStackTree tree,
                                   HashSet<CallStackNode> roots)
    {
        TestOutputHelper.WriteLine("");
        TestOutputHelper.WriteLine($"=== {label} ===");

        var leaves = selected.Where(e => e.CallStack is not null).Select(e => e.CallStack!).Distinct().ToList();

        TestOutputHelper.WriteLine($"leaves with call stack: {leaves.Count}");

        var visible = new HashSet<CallStackNode>();

        foreach (var leaf in leaves)
        {
            var pathLen = 0;
            var nonInfra = 0;
            CallStackNode? top = null;

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
        var reachable = new HashSet<CallStackNode>();

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
    private void RenderScoped(CallStackNode node, HashSet<CallStackNode> visible, int depth)
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

    private static int CountVisibleTopDown(CallStackNode node,
                                           HashSet<CallStackNode> visible,
                                           HashSet<CallStackNode> reachable)
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
        var pdbs = result.CallStackTree?
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

            using var resolver = new Query.CallStack.Dia.DiaResolver(pdbPath);

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

        if (result.CallStackTree is not { } tree)
        {
            TestOutputHelper.WriteLine("No call stack captured (is C:\\Symbols available?)");

            return;
        }

        TestOutputHelper.WriteLine($"Roots: {tree.Root.ChildNodes.Count()}   Nodes: {tree.Nodes().Count()}");
        TestOutputHelper.WriteLine("");
        TestOutputHelper.WriteLine(tree.Render());
    }

    [Fact]
    public async Task Diagnose_Crop_Keeps_Plan_Handle_Reads()
    {
        var result = await RunQuery("SELECT TOP 10 * FROM dbo.ClusteredTable");

        var readGroups = result.EngineEvents.OfType<ReadEventGroup>().Count();

        var withStack = result.EngineEvents.Count(e => e.CallStack is not null)
                        + result.EngineEvents.OfType<ReadEventGroup>().Sum(g => g.Events.Count(c => c.CallStack is not null));

        var roots = result.CallStackTree?.Root.ChildNodes.Count() ?? 0;
        var nodes = result.CallStackTree?.Nodes().Count() ?? 0;

        TestOutputHelper.WriteLine($"crop window={result.CropStartUs}..{result.CropEndUs}  events={result.EngineEvents.Count}"
            + $"  readGroups={readGroups}  eventsWithStack={withStack}  treeRoots={roots}  treeNodes={nodes}");
    }

    [Fact]
    public async Task Diagnose_Lock_Escalation()
    {
        // UPDLOCK + HOLDLOCK takes RS_U range key locks and holds them; with enough rows it escalates to an object lock.
        // The question: do the key locks get a lock_released (so they pair), or are they unpaired (no release captured)?
        var database = await LoadDatabase();

        var result = await RunQuery(
            """
            SELECT *
            FROM dbo.LockEscalationDemo WITH (UPDLOCK, HOLDLOCK)
            WHERE Category = 1;
            """, database);

        var locks = result.EngineEvents.OfType<LockEvent>()
            .Concat(result.EngineEvents.OfType<Query.Events.Locks.LockGroup>().SelectMany(g => g.Events.OfType<LockEvent>()))
            .ToList();

        var groups = result.EngineEvents.OfType<Query.Events.Locks.LockGroup>().ToList();

        // After IntervalCollapser a paired lock is renamed "Lock"; an unpaired acquire stays "lock_acquired"; an
        // unpaired release stays "lock_released". So the names tell us the pairing outcome directly.
        TestOutputHelper.WriteLine($"locks={locks.Count}  lockGroups={groups.Count}  "
            + $"paired(Lock)={locks.Count(l => l.Name == "Lock")}  "
            + $"unpairedAcquire={locks.Count(l => l.Name == "lock_acquired")}  "
            + $"unpairedRelease={locks.Count(l => l.Name == "lock_released")}");

        // Is a single group correct? Show how many distinct (object, transaction) pairs the locks actually span, and the
        // composition of each group — a group should be exactly one object + one transaction.
        var distinctKeys = locks
            .Select(l => (Obj: l.AllocationUnit?.ObjectId ?? 0, Txn: l.LockOwnerContext?.TransactionId ?? 0))
            .Distinct()
            .ToList();

        TestOutputHelper.WriteLine($"distinct (object,transaction) pairs across all locks = {distinctKeys.Count}");

        var topLevelLocks = result.EngineEvents.OfType<LockEvent>().Count();

        TestOutputHelper.WriteLine($"top-level (ungrouped) locks left over = {topLevelLocks}");

        foreach (var group in groups)
        {
            var members = group.Events.OfType<LockEvent>().ToList();

            var objects = members.Select(l => l.AllocationUnit?.ObjectId ?? 0).Distinct().ToList();
            var txns = members.Select(l => l.LockOwnerContext?.TransactionId ?? 0).Distinct().ToList();

            TestOutputHelper.WriteLine($"  group '{group.Description}' members={members.Count} "
                + $"objects=[{string.Join(",", objects.Take(4))}] transactions=[{string.Join(",", txns.Take(4))}]");
        }

        // Do the fine key locks end at the escalation (object X acquire), well before the statement end?
        var keys = locks.Where(l => l.Resource.ResourceType == Query.Events.Locks.LockResourceType.Key).ToList();

        // KEY locks carry their HoBT in resource_2 — after the parse fix these should be populated (was all 0).
        TestOutputHelper.WriteLine($"key locks={keys.Count}  withHobtId={keys.Count(l => l.Resource.HobtId is > 0)}  "
            + $"withAllocationUnit={keys.Count(l => l.AllocationUnit is not null)}  "
            + $"distinctHobt=[{string.Join(",", keys.Select(l => l.Resource.HobtId).Distinct().Take(4))}]");

        var objectX = locks.Where(l => l.Resource.ResourceType == Query.Events.Locks.LockResourceType.Object
                                       && l.LockMode == Query.Events.Locks.LockMode.X).ToList();

        if (keys.Count > 0 && objectX.Count > 0)
        {
            var escalationTime = objectX.Min(l => l.TimeUs);

            // Fine key locks in the escalating transaction should end AT the escalation, not at the statement end.
            TestOutputHelper.WriteLine($"key lock span={keys.Min(l => l.TimeUs)}..{keys.Max(l => l.TimeUs + l.DurationUs)}  "
                + $"escalation(objectX acquire)={escalationTime}  statementEnd~={locks.Max(l => l.TimeUs)}  "
                + $"keysEndingAtEscalation={keys.Count(l => Math.Abs(l.TimeUs + l.DurationUs - escalationTime) < 5000)}/{keys.Count}");
        }

        // Per (type, mode, name): count, and how many have a duration (paired => non-zero).
        foreach (var g in locks
                     .GroupBy(l => (l.Resource.ResourceType, l.LockMode, l.Name))
                     .OrderByDescending(g => g.Count()))
        {
            var (type, mode, name) = g.Key;

            TestOutputHelper.WriteLine($"  type={type,-8} mode={mode,-5} name='{name,-13}' count={g.Count(),-5} "
                + $"withDuration={g.Count(l => l.DurationUs > 0)}");
        }
    }

    [Fact]
    public async Task Diagnose_Raw_Lock_Events()
    {
        // Read the RAW .xel (pre-consolidation) and count lock_acquired vs lock_released by resource type + mode. TOP 10
        // stays under the escalation threshold, so the RS_U keys should be released at commit (captured) rather than
        // bulk-dropped by escalation — i.e. this should show RS_U releases the full-table run did not.
        var result = await RunQuery(
            """
            SELECT TOP 10 *
            FROM dbo.LockEscalationDemo WITH (UPDLOCK, HOLDLOCK)
            WHERE Category = 1;
            """);

        var connectionString = ConnectionStringHelper.GetConnectionString("Local");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);

        string? logPath;

        await using (var locationCommand = new SqlCommand(EventSql.GetFileLocationSql(), connection))
        {
            logPath = (string?)await locationCommand.ExecuteScalarAsync(CancellationToken.None);
        }

        var path = $"{logPath}\\{result.SessionId}*.xel";

        TestOutputHelper.WriteLine($"reading {path}");

        const string sql = """
            SELECT object_name AS event_name,
                   CONVERT(XML, event_data).value('(event/data[@name="resource_type"]/text)[1]', 'varchar(30)') AS resource_type,
                   CONVERT(XML, event_data).value('(event/data[@name="mode"]/text)[1]', 'varchar(30)') AS mode
            FROM sys.fn_xe_file_target_read_file(@p, NULL, NULL, NULL)
            WHERE object_name IN ('lock_acquired', 'lock_released')
            """;

        var counts = new Dictionary<(string Name, string ResourceType, string Mode), int>();

        await using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("@p", path);

            await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);

            while (await reader.ReadAsync(CancellationToken.None))
            {
                var key = (reader.GetString(0),
                           reader.IsDBNull(1) ? "?" : reader.GetString(1),
                           reader.IsDBNull(2) ? "?" : reader.GetString(2));

                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
        }

        foreach (var (key, count) in counts.OrderBy(k => k.Key.ResourceType).ThenBy(k => k.Key.Name))
        {
            TestOutputHelper.WriteLine($"  {key.Name,-14} resource_type={key.ResourceType,-10} mode={key.Mode,-6} count={count}");
        }
    }

    [Fact]
    public async Task Diagnose_Key_Lock_Hash()
    {
        var database = await LoadDatabase();

        var result = await RunQuery(
            """
            SELECT *
            FROM dbo.LockEscalationDemo WITH (UPDLOCK, HOLDLOCK)
            WHERE Category = 1;
            """, database);

        var keys = result.EngineEvents.OfType<LockEvent>()
            .Concat(result.EngineEvents.OfType<Query.Events.Locks.LockGroup>().SelectMany(g => g.Events.OfType<LockEvent>()))
            .Where(l => l.Resource.ResourceType == Query.Events.Locks.LockResourceType.Key)
            .ToList();

        var ourHashes = keys.Select(l => l.Resource.KeyHash).Where(h => h is not null).Distinct().ToHashSet();

        // End-to-end: GetEventKeyAddresses (run in the pipeline) matches our KeyHash against the table's %%lockres%% and
        // sets RowIdentifier — so a resolved RowIdentifier proves our hash format matches what SQL computes.
        var resolved = keys.Count(l => l.Resource.RowIdentifier is not null);

        TestOutputHelper.WriteLine($"key locks={keys.Count}  distinct hashes={ourHashes.Count}  "
            + $"rowIdentifierResolved={resolved}");

        // Direct check: how many of our hashes SQL finds against the table's clustered %%lockres%%.
        var connectionString = ConnectionStringHelper.GetConnectionString("Local");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);

        await using var command = new SqlCommand(
            "SELECT %%lockres%% AS hash FROM dbo.LockEscalationDemo", connection);

        var realHashes = new HashSet<string>();

        await using (var reader = await command.ExecuteReaderAsync(CancellationToken.None))
        {
            while (await reader.ReadAsync(CancellationToken.None))
            {
                realHashes.Add(reader.GetString(0));
            }
        }

        TestOutputHelper.WriteLine($"table rows={realHashes.Count}  ourHashesFoundInTable={ourHashes.Count(h => realHashes.Contains(h!))}");
        TestOutputHelper.WriteLine($"sample ours = [{string.Join(", ", ourHashes.Take(4))}]");
    }

    [Fact]
    public async Task Diagnose_Lock_Pairing()
    {
        // HOLDLOCK holds key/page/object locks to the end of the statement, so there are real (non-metadata) locks to
        // inspect for allocation-unit resolution.
        var result = await RunQuery("SELECT * FROM dbo.ClusteredTable WITH (HOLDLOCK)");

        // Locks are now inside LockGroups; flatten those back out plus any top-level (ungrouped) locks.
        var locks = result.EngineEvents.OfType<LockEvent>()
            .Concat(result.EngineEvents.OfType<Query.Events.Locks.LockGroup>().SelectMany(g => g.Events.OfType<LockEvent>()))
            .ToList();

        var withPlanHandle = locks.Count(l => l.PlanHandleId != 0);

        var lockStart = locks.Count > 0 ? locks.Min(l => l.TimeUs) : 0;
        var lockEnd = locks.Count > 0 ? locks.Max(l => l.TimeUs + l.DurationUs) : 0;

        TestOutputHelper.WriteLine($"locks={locks.Count}  withPlanHandle={withPlanHandle}");
        TestOutputHelper.WriteLine($"crop window={result.CropStartUs}..{result.CropEndUs}   lock span={lockStart}..{lockEnd}");

        // Per resource type: distinct object_ids / HoBT ids (uniform object key?), and how many carry a plan_handle.
        foreach (var g in locks.GroupBy(l => l.Resource.ResourceType))
        {
            var objIds = g.Select(l => l.Resource.ObjectId).Distinct().OrderBy(x => x).ToList();

            TestOutputHelper.WriteLine($"  type={g.Key,-10} count={g.Count(),-5} withPlanHandle={g.Count(l => l.PlanHandleId != 0),-5} "
                + $"objectIds=[{string.Join(",", objIds.Take(6))}]");
        }
    }

    [Fact]
    public async Task Diagnose_Scan_Latches_Vs_Reads()
    {
        // A full clustered-index scan of a warm table: most pages are already resident, so their access should show as
        // BUF SH cached reads, not bare latches. This dumps what actually survives read grouping so we can see whether
        // the "purple" latches are BUF/SH/latch_acquired (which SHOULD have grouped) or some other class/mode/name.
        //
        // Warm the buffer pool first (its own trace run) so the MEASURED run reads resident pages (the cached path),
        // rather than loading them from disk as non-cached reads. Point this at whatever large table reproduces the
        // scan you are investigating; dbo.ClusteredTable is the standard fixture used by the other diagnostics here.
        const string sql = "SELECT * FROM dbo.ClusteredTable";

        await RunQuery(sql);

        var result = await RunQuery(sql);

        var reads = result.EngineEvents.OfType<ReadEventGroup>().ToList();

        TestOutputHelper.WriteLine($"crop window={result.CropStartUs}..{result.CropEndUs}  events={result.EngineEvents.Count}");
        TestOutputHelper.WriteLine($"reads={reads.Count}  cached={reads.Count(r => r.ReadType == ReadType.Cached)}  "
            + $"nonCached={reads.Count(r => r.ReadType == ReadType.NonCached)}");

        if (reads.Count > 0)
        {
            var readStart = reads.Min(r => r.TimeUs);
            var readEnd = reads.Max(r => r.TimeUs + r.DurationUs);

            var withTable = reads.Count(r => !string.IsNullOrEmpty(r.TableName));
            var matched = reads.Count(r => r.PlanNodeIdentifier is not null);

            TestOutputHelper.WriteLine($"read span={readStart}..{readEnd}  withTableName={withTable}  matchedToNode={matched}");
        }

        // Every operator's span (the statement/SELECT node is the one that drives the crop window).
        foreach (var op in result.EngineEvents.OfType<Query.Events.Operators.ExecutionOperatorEvent>())
        {
            TestOutputHelper.WriteLine($"operator node={op.PlanNodeIdentifier?.NodeId,3} '{op.Name}' "
                + $"TimeUs={op.TimeUs} end={op.TimeUs + op.DurationUs}");
        }

        // What actually occupies the TAIL — everything ending latest, so we can see what generates the gap past the
        // last read. Grouped by type with the max end per type.
        TestOutputHelper.WriteLine("");
        TestOutputHelper.WriteLine("latest end per event type:");

        // query_thread_profile / memory events are end-anchored (TimeUs is the close), so their real end is TimeUs.
        static long EndUs(EngineEvent e) =>
            e is Query.Events.Operators.QueryThreadEvent or MemoryEvent ? e.TimeUs : e.TimeUs + e.DurationUs;

        foreach (var g in result.EngineEvents
                     .GroupBy(e => e.GetType().Name)
                     .Select(g => (Type: g.Key, End: g.Max(EndUs), Count: g.Count()))
                     .OrderByDescending(x => x.End))
        {
            TestOutputHelper.WriteLine($"  {g.Type,-24} maxEnd={g.End,-10} count={g.Count}");
        }

        // Where do BUF SH latches actually end up? Count them in three buckets: top-level (ungrouped — should be a
        // cached read), inside a NON-cached read (folded as the just-loaded-page tail), inside a cached read.
        var topLevelLatches = result.EngineEvents.OfType<LatchEvent>().ToList();

        var bufShTop = topLevelLatches.Count(IsBufSh);

        var membersByReadType = reads
            .SelectMany(r => r.Events.OfType<LatchEvent>().Where(IsBufSh).Select(_ => r.ReadType))
            .ToList();

        TestOutputHelper.WriteLine("");
        TestOutputHelper.WriteLine($"BUF SH latch_acquired:  topLevel(ungrouped)={bufShTop}  "
            + $"insideNonCachedRead={membersByReadType.Count(t => t == ReadType.NonCached)}  "
            + $"insideCachedRead={membersByReadType.Count(t => t == ReadType.Cached)}");

        // Every TOP-LEVEL latch that survived grouping, bucketed by (class, mode, name). A BUF/SH/latch_acquired row
        // here is a bug — it should have been consumed into a cached read.
        TestOutputHelper.WriteLine("");
        TestOutputHelper.WriteLine($"ungrouped top-level latches={topLevelLatches.Count}");

        foreach (var g in topLevelLatches
                     .GroupBy(l => (l.LatchClass, l.LatchMode, l.Name))
                     .OrderByDescending(g => g.Count()))
        {
            var (latchClass, mode, name) = g.Key;

            var withPage = g.Count(l => l.PageAddress is { PageId: > 0 });
            var withPlanHandle = g.Count(l => l.PlanHandleId != 0);

            TestOutputHelper.WriteLine($"  class={latchClass,-10} mode={mode,-3} name='{name}' count={g.Count(),-5} "
                + $"withPage={withPage,-5} withPlanHandle={withPlanHandle}");
        }
    }

    private static bool IsBufSh(LatchEvent l) =>
        l is { LatchClass: LatchClass.BUF, LatchMode: LatchMode.SH, Name: "latch_acquired" };

    private async Task<QueryResult> RunQuery(string sql, DatabaseSource? database = null)
    {
        var logger = TestLogger.GetLogger<QueryRunner>(TestOutputHelper, LogLevel.Information);

        var connectionString = ConnectionStringHelper.GetConnectionString("Local");

        var eventReader = new EventReader(TestLogger.GetLogger<EventReader>(TestOutputHelper, LogLevel.Information));

        var logReader = new LogRecordReader(TestLogger.GetLogger<LogRecordReader>(TestOutputHelper, LogLevel.Information));
        var executor = new QueryRunner(logger, eventReader, logReader);

        // A bare DatabaseSource loads no allocation units, so lock/read events resolve no object; pass a LoadDatabase()
        // result to exercise the full HoBT/object -> allocation-unit path the way the app does.
        database ??= new DatabaseSource(
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

    // Loads a real DatabaseSource (metadata + allocation units) from the "Local" connection, so lock/read events resolve
    // their allocation unit exactly as in the app — the bare DatabaseSource in RunQuery loads none.
    private async Task<DatabaseSource> LoadDatabase()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("Local");

        using var host = Host.CreateDefaultBuilder()
                             .ConfigureServices((_, services) => services.RegisterServices())
                             .Build();

        var databaseService = host.Services.GetRequiredService<IDatabaseService>();

        var connection = new ServerConnectionFactory(TestLogger.GetLogger<QueryPageReader>(TestOutputHelper))
            .Create(c => c.ConnectionString = connectionString);

        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        return await databaseService.LoadAsync(databaseName, connection, CancellationToken.None);
    }
}
