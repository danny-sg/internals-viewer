using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using Xunit.Abstractions;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class CallStackTreeTests(ITestOutputHelper output)
{
    [Fact]
    public void Truncated_Stack_Merges_Onto_The_Fuller_Path()
    {
        var tree = new CallStackTree();

        // Frames are innermost-first: event site Y, then X, ThreadEntryPoint, and (outermost) BaseThreadInitThunk.
        tree.Add([Frame("Y", 10), Frame("X", 20), Frame("ThreadEntryPoint", 30), Frame("BaseThreadInitThunk", 40)], Event());

        // The same path but truncated at the outer end (BaseThreadInitThunk dropped, as a deep capture would be).
        tree.Add([Frame("Y", 10), Frame("X", 20), Frame("ThreadEntryPoint", 30)], Event());

        var collapsed = tree.CollapseToFunctions();

        var text = collapsed.Render();

        output.WriteLine(text);

        // The truncated stack grafts on: ThreadEntryPoint appears once, under BaseThreadInitThunk, both events reach Y.
        Assert.Equal(
            """
            BaseThreadInitThunk::m
              ThreadEntryPoint::m
                X::m
                  Y::m [2]

            """.ReplaceLineEndings("\n"),
            text.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void A_Truncated_Stack_Grafts_Onto_The_Copy_That_Calls_What_It_Calls()
    {
        // FExecute recurs — the engine re-enters it when a batch is auto-parameterised — so a truncated stack rooted
        // there has two copies to choose from. Only the inner one calls Stmt; picking by function name alone is a coin
        // toss, and losing it files this whole subtree under a call path that never ran.
        var tree = new CallStackTree();

        tree.Add([Frame("Prep", 10), Frame("Loop", 20), Frame("FExecute", 30), Frame("Execute", 40)], Event());

        tree.Add([Frame("Stmt", 50), Frame("FExecute", 60), Frame("Execute", 70), Frame("Prep", 10), Frame("Loop", 20),
                  Frame("FExecute", 30), Frame("Execute", 40)], Event());

        // The same statement captured without the outer frames.
        tree.Add([Frame("Stmt", 50), Frame("FExecute", 60)], Event());

        var collapsed = tree.CollapseToFunctions();

        var grafted = collapsed.Nodes().Single(node => node.Symbol == "Stmt::m");

        // Under the INNER FExecute (the one reached through Prep), not the outer one it shares a name with.
        Assert.Equal("FExecute::m", grafted.Parent?.Symbol);
        Assert.Equal("Execute::m", grafted.Parent?.Parent?.Symbol);
        Assert.Equal("Prep::m", grafted.Parent?.Parent?.Parent?.Symbol);
    }

    [Fact]
    public void A_Truncated_Leaf_Grafts_Even_Though_Nothing_Says_Where_It_Belongs()
    {
        // A single captured frame — the event site with nothing below it — is the common shape of a truncated stack,
        // and it carries no callees to choose a parent by. Withholding the graft for want of a signal strands nearly
        // every truncated stack at the root and empties the tree, so an imperfect parent wins over no tree at all.
        var tree = new CallStackTree();

        tree.Add([Frame("Y", 10), Frame("FExecute", 30), Frame("Execute", 40)], Event());

        tree.Add([Frame("Z", 20), Frame("FExecute", 60), Frame("Other", 80), Frame("Execute", 40)], Event());

        tree.Add([Frame("FExecute", 70)], Event());

        var collapsed = tree.CollapseToFunctions();

        Assert.DoesNotContain(collapsed.Root.ChildNodes, node => node.Symbol == "FExecute::m");
    }

    [Fact]
    public void Every_Event_Is_Reachable_From_The_Root_After_Grafting()
    {
        // Grafting picks its target from a list built before any of it runs, and each merge discards the nodes whose
        // keys collided with its target's. A later truncated stack can therefore be grafted onto a copy that an earlier
        // merge already merged away — hanging it off a node nothing can reach. It fails silently: no error, no root, the
        // frames simply never render, and an operator's entry frame vanishes with them.
        var tree = new CallStackTree();

        // Two full paths that share their inner frames, so merging one discards the other's copies of them.
        tree.Add([Frame("Leaf", 10), Frame("Inner", 20), Frame("Outer", 30), Frame("ThreadA", 40)], Event());
        tree.Add([Frame("Leaf", 11), Frame("Inner", 21), Frame("Outer", 31), Frame("ThreadB", 50)], Event());

        // Truncated captures of each, rooted at the shared frames.
        tree.Add([Frame("Leaf", 12), Frame("Inner", 22)], Event());
        tree.Add([Frame("Leaf", 13), Frame("Inner", 23), Frame("Outer", 32)], Event());

        var collapsed = tree.CollapseToFunctions();

        var reachable = collapsed.Nodes().ToHashSet();

        foreach (var node in reachable)
        {
            foreach (var engineEvent in node.Events)
            {
                Assert.True(reachable.Contains(engineEvent.CallStack!),
                            $"'{engineEvent.CallStack?.Symbol}' is not reachable from the root");
            }
        }

        // And nothing was dropped on the way: every event still reaches a node the tree can walk to.
        Assert.Equal(4, reachable.Sum(n => n.Events.Count));
    }

    [Fact]
    public void A_Recursive_Truncated_Stack_Does_Not_Create_A_Cycle()
    {
        var tree = new CallStackTree();

        // Innermost-first: site Y, then A, B, A — the OUTERMOST frame (A) recurs deeper in the same stack. Truncated
        // (no thread-entry frames above), so on collapse A is a root child that also appears inside its own subtree.
        // Grafting the root child onto that inner copy would make it its own ancestor — a cycle. The graft must be
        // skipped, and the collapse must terminate with an acyclic tree.
        tree.Add([Frame("Y", 10), Frame("A", 20), Frame("B", 30), Frame("A", 40)], Event());

        var collapsed = tree.CollapseToFunctions();

        // Every node's parent chain reaches the root without revisiting — i.e. no cycle (this walk would hang otherwise).
        foreach (var node in collapsed.Nodes())
        {
            var seen = new HashSet<CallStackNode>();

            for (var current = node; current is not null; current = current.Parent)
            {
                Assert.True(seen.Add(current), "call-stack tree parent chain contains a cycle");
            }
        }
    }

    [Fact]
    public void A_Projection_Carries_Only_The_Included_Events()
    {
        // Two events reaching the SAME leaf, as two operators of one kind do — same code, same addresses, so the
        // collapse merges them onto one node holding both. Projecting one of them must not bring the other along:
        // showing a scope's frames with the whole query's event counts on them is the thing projection exists to fix.
        var tree = new CallStackTree();

        var mine = Event();

        var theirs = Event();

        tree.Add([Frame("Y", 10), Frame("X", 20)], mine);
        tree.Add([Frame("Y", 10), Frame("X", 20)], theirs);

        var collapsed = tree.CollapseToFunctions();

        Assert.Equal(2, collapsed.Nodes().Single(n => n.Events.Count > 0).Events.Count);

        var projected = collapsed.Project(include: e => ReferenceEquals(e, mine));

        Assert.Same(mine, Assert.Single(projected.Nodes().Single(n => n.Events.Count > 0).Events));
    }

    [Fact]
    public void A_Cut_Projection_Is_Rooted_At_The_Boundary()
    {
        var tree = new CallStackTree();

        tree.Add([Frame("Y", 10), Frame("Seek", 20), Frame("Join", 30), Frame("Thread", 40)], Event());

        var collapsed = tree.CollapseToFunctions();

        var boundary = collapsed.Nodes().Single(n => n.Symbol == "Seek::m");

        var projected = collapsed.Project(cutAt: node => ReferenceEquals(node, boundary));

        // Rooted at Seek, holding its own work (Y) — the path it was reached BY (Join, Thread) is gone.
        Assert.Equal(
            """
            Seek::m
              Y::m [1]

            """.ReplaceLineEndings("\n"),
            projected.Render().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Cut_Segments_Of_One_Operator_Merge_Rather_Than_Nest()
    {
        // One operator entered from two worker stacks, as a parallel operator is. Cutting gives two segments that are
        // the same function, so they merge into a single root holding both workers' work — the tree stays about the
        // operator, not about which thread got there. Nesting one under the other (grafting, which reunites a truncated
        // capture with its fuller path) would be wrong here: these roots are truncated deliberately.
        var tree = new CallStackTree();

        tree.Add([Frame("Y", 10), Frame("Seek", 20), Frame("WorkerA", 30)], Event());
        tree.Add([Frame("Z", 11), Frame("Seek", 20), Frame("WorkerB", 31)], Event());

        var collapsed = tree.CollapseToFunctions();

        var boundaries = collapsed.Nodes().Where(n => n.Symbol == "Seek::m").ToList();

        Assert.Equal(2, boundaries.Count);

        var projected = collapsed.Project(cutAt: boundaries.Contains);

        Assert.Equal(
            """
            Seek::m
              Y::m [1]
              Z::m [1]

            """.ReplaceLineEndings("\n"),
            projected.Render().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void A_Segment_Ends_At_The_Outermost_Nested_Operator_It_Crosses()
    {
        // The statement's exits are the entry frames of EVERY operator beneath it, so a read crosses several of them on
        // the way up: the seek's first, then the aggregate's. Ending at the first one found would resume the statement's
        // segment INSIDE the aggregate and hand it the aggregate's work — the thing being excluded. It must end at the
        // outermost one crossed.
        var tree = new CallStackTree();

        tree.Add([Frame("BufRead", 10), Frame("Seek", 20), Frame("SeekInternals", 30), Frame("Agg", 40),
                  Frame("Statement", 50)], Event());

        var collapsed = tree.CollapseToFunctions();

        var statement = collapsed.Nodes().Single(n => n.Symbol == "Statement::m");

        var exits = collapsed.Nodes().Where(n => n.Symbol is "Agg::m" or "Seek::m").ToList();

        var projected = collapsed.Project(cutAt: node => ReferenceEquals(node, statement), stopBelow: exits.Contains);

        // Down to Agg, and no further: SeekInternals is the aggregate's work, and the read is the seek's.
        Assert.Equal(
            """
            Statement::m

            """.ReplaceLineEndings("\n"),
            projected.Render().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void A_Segment_Keeps_The_Events_Of_A_Leaf_It_Reaches_Unbroken()
    {
        // The innermost operator has nothing nested inside it, so its segment runs down to the leaf and owns its events
        // — the counterpart to the crossing case, where the events belong to the nested operator instead.
        var tree = new CallStackTree();

        tree.Add([Frame("BufRead", 10), Frame("Seek", 20), Frame("Agg", 30)], Event());

        var collapsed = tree.CollapseToFunctions();

        var seek = collapsed.Nodes().Single(n => n.Symbol == "Seek::m");

        var projected = collapsed.Project(cutAt: node => ReferenceEquals(node, seek), stopBelow: _ => false);

        Assert.Equal(
            """
            Seek::m
              BufRead::m [1]

            """.ReplaceLineEndings("\n"),
            projected.Render().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Two_Events_Through_One_Barrier_Frame_Scope_Separately()
    {
        // The barrier says where to stop climbing; the EVENT says whose stack it is. Two reads share the barrier node —
        // the collapse merges them, since it is the same function — so if the scope came from the frame they would show
        // each other's work. It comes from the event, so they do not.
        var tree = new CallStackTree();

        var first = Event();

        var second = Event();

        tree.Add([Frame("Latch", 10), Frame("BPool", 20), Frame("GetPageWithKey", 30), Frame("Seek", 40)], first);
        tree.Add([Frame("Latch", 10), Frame("BPool", 20), Frame("GetPageWithKey", 30), Frame("Seek", 40)], second);

        var collapsed = tree.CollapseToFunctions();

        var barrier = collapsed.Nodes().Single(n => n.Symbol == "GetPageWithKey::m");

        var projected = collapsed.Project(include: e => ReferenceEquals(e, first),
                                          cutAt: node => ReferenceEquals(node, barrier));

        Assert.Equal(
            """
            GetPageWithKey::m
              BPool::m
                Latch::m [1]

            """.ReplaceLineEndings("\n"),
            projected.Render().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void An_Events_Stack_Cuts_At_The_Nearest_Barrier_Above_It()
    {
        // Nearest-above: a barrier marked deeper wins over one marked higher, so the stack is the smallest unit of work
        // that contains the event rather than everything back to the outermost marked frame.
        var tree = new CallStackTree();

        tree.Add([Frame("Latch", 10), Frame("BPool", 20), Frame("Seek", 30), Frame("GetPageWithKey", 40)], Event());

        var collapsed = tree.CollapseToFunctions();

        var barriers = collapsed.Nodes().Where(n => n.Symbol is "GetPageWithKey::m" or "BPool::m").ToList();

        var projected = collapsed.Project(cutAt: barriers.Contains);

        Assert.Equal(
            """
            BPool::m
              Latch::m [1]

            """.ReplaceLineEndings("\n"),
            projected.Render().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void A_Projection_Leaves_The_Events_Pointing_At_The_Shared_Tree()
    {
        // EngineEvent.CallStack has to keep naming the one shared tree — every per-scope projection is a throwaway view
        // of it, so a projection that repointed would leave the events indexing whichever was built last.
        var tree = new CallStackTree();

        var engineEvent = Event();

        tree.Add([Frame("Y", 10), Frame("X", 20)], engineEvent);

        var collapsed = tree.CollapseToFunctions();

        var shared = engineEvent.CallStack;

        collapsed.Project();

        Assert.Same(shared, engineEvent.CallStack);
    }

    private static int _sequence;

    private static EngineEvent Event() => new() { Name = "e", SequenceId = _sequence++ };

    private static CallstackFrame Frame(string name, uint rva) => new()
    {
        Module = "sqllang",
        Rva = rva,
        Resolved = new ResolvedCallstackFrame { ClassName = name, MethodName = "m" },
    };
}
