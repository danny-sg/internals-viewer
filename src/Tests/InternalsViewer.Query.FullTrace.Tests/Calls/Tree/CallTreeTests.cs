using InternalsViewer.Query.FullTrace.Calls.Tree;

namespace InternalsViewer.Query.FullTrace.Tests.Calls.Tree;

[Trait("Category", "Unit")]
public class CallTreeTests
{
    [Fact]
    public void Removing_Subtrees_Takes_Each_Matching_Node_With_Everything_Under_It()
    {
        var tree = new CallTree();

        var caller = tree.AddCall(tree.Root, Frame("Caller", 0x10), 1);
        var removed = tree.AddCall(caller, Frame("Removed", 0x20), 1);
        var callee = tree.AddCall(removed, Frame("Callee", 0x30), 1);
        var sibling = tree.AddCall(caller, Frame("Sibling", 0x40), 1);

        var cut = tree.RemoveSubtrees(n => n.Frame?.Address == 0x20);

        Assert.Equal([removed, callee], cut.OrderBy(n => n.Frame!.Address));
        Assert.Equal([caller, sibling], tree.GetNodes().OrderBy(n => n.Frame!.Address));
    }

    [Fact]
    public void Merged_Nodes_Add_Up_Their_Calls_And_Allocations_And_Keep_Their_Members()
    {
        var tree = new CallTree();

        var first = tree.AddCall(tree.AddCall(tree.Root, new CallFrame { Module = new("sqlmin"), Address = 0x10, Rva = 0x10 }, 1),
                                 new CallFrame { Module = new("sqlmin"), Address = 0x30, Rva = 0x30 },
                                 2);

        var second = tree.AddCall(tree.AddCall(tree.Root, new CallFrame { Module = new("sqlmin"), Address = 0x20, Rva = 0x20 }, 1),
                                  new CallFrame { Module = new("sqlmin"), Address = 0x30, Rva = 0x30 },
                                  3);

        var callee = tree.AddCall(second, new CallFrame { Module = new("sqlmin"), Address = 0x40, Rva = 0x40 }, 1);

        first.AllocatedBytes = 100;
        first.AllocationCount = 1;
        callee.AllocatedBytes = 50;
        callee.AllocationCount = 2;

        var merged = CallTreeNode.Merge([first, second]);

        Assert.Equal((5L, 150L, 3L), (merged.CallCount, merged.AllocatedBytesIncludingChildren, merged.AllocationCountIncludingChildren));
        Assert.Equal([first, second], merged.MergedFrom);
        Assert.Equal(first.Key, merged.Key);
        Assert.Empty(merged.Children);
    }

    [Fact]
    public void A_Cut_Projection_Is_Rooted_At_The_Boundary()
    {
        var tree = new CallTree();

        Path(tree, Frame("Thread", 40), Frame("Join", 30), Frame("Seek", 20), Frame("Y", 10));

        var collapsed = tree.Project();

        var boundary = collapsed.GetNodes().Single(n => n.Symbol == "Seek::m");

        var projected = collapsed.Project(cutAt: node => ReferenceEquals(node, boundary));

        // Rooted at Seek, holding its own work (Y) — the path it was reached BY (Join, Thread) is gone.
        Assert.Equal(
            """
            Seek::m
              Y::m (1 calls)

            """.ReplaceLineEndings("\n"),
            Render(projected).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Cut_Segments_Of_One_Operator_Merge_Rather_Than_Nest()
    {
        // One operator entered from two worker stacks, as a parallel operator is. Cutting gives two segments that are
        // the same function, so they merge into a single root holding both workers' work — the tree stays about the
        // operator, not about which thread got there. Nesting one under the other (grafting, which reunites a truncated
        // capture with its fuller path) would be wrong here: these roots are truncated deliberately.
        var tree = new CallTree();

        Path(tree, Frame("WorkerA", 30), Frame("Seek", 20), Frame("Y", 10));
        Path(tree, Frame("WorkerB", 31), Frame("Seek", 20), Frame("Z", 11));

        var collapsed = tree.Project();

        var boundaries = collapsed.GetNodes().Where(n => n.Symbol == "Seek::m").ToList();

        Assert.Equal(2, boundaries.Count);

        var projected = collapsed.Project(cutAt: boundaries.Contains);

        Assert.Equal(
            """
            Seek::m
              Y::m (1 calls)
              Z::m (1 calls)

            """.ReplaceLineEndings("\n"),
            Render(projected).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void A_Segment_Ends_At_The_Outermost_Nested_Operator_It_Crosses()
    {
        // The statement's exits are the entry frames of EVERY operator beneath it, so a read crosses several of them on
        // the way up: the seek's first, then the aggregate's. Ending at the first one found would resume the statement's
        // segment INSIDE the aggregate and hand it the aggregate's work — the thing being excluded. It must end at the
        // outermost one crossed.
        var tree = new CallTree();

        Path(tree, Frame("Statement", 50), Frame("Agg", 40), Frame("SeekInternals", 30), Frame("Seek", 20), Frame("BufRead", 10));

        var collapsed = tree.Project();

        var statement = collapsed.GetNodes().Single(n => n.Symbol == "Statement::m");

        var exits = collapsed.GetNodes().Where(n => n.Symbol is "Agg::m" or "Seek::m").ToList();

        var projected = collapsed.Project(cutAt: node => ReferenceEquals(node, statement), stopBelow: exits.Contains);

        // Down to Agg, and no further: SeekInternals is the aggregate's work, and the read is the seek's.
        Assert.Equal(
            """
            Statement::m

            """.ReplaceLineEndings("\n"),
            Render(projected).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void A_Segment_Keeps_The_Calls_Of_A_Leaf_It_Reaches_Unbroken()
    {
        // The innermost operator has nothing nested inside it, so its segment runs down to the leaf and owns its events
        // — the counterpart to the crossing case, where the events belong to the nested operator instead.
        var tree = new CallTree();

        Path(tree, Frame("Agg", 30), Frame("Seek", 20), Frame("BufRead", 10));

        var collapsed = tree.Project();

        var seek = collapsed.GetNodes().Single(n => n.Symbol == "Seek::m");

        var projected = collapsed.Project(cutAt: node => ReferenceEquals(node, seek), stopBelow: _ => false);

        Assert.Equal(
            """
            Seek::m
              BufRead::m (1 calls)

            """.ReplaceLineEndings("\n"),
            Render(projected).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Trace_Activity_Survives_The_Collapse_And_Sums_Up_The_Tree()
    {
        var tree = new CallTree();

        var x = tree.AddCall(tree.Root, Frame("X", 100), 1);

        var y = tree.AddCall(x, Frame("Y", 200), 4);

        x.OwnActivityCounts = [1, 0, 0];

        y.OwnActivityCounts = [0, 3, 1];

        var collapsed = tree.Project();

        collapsed.ComputeActivity(3);

        var root = collapsed.GetNodes().Single(node => node.Symbol == "X::m");

        Assert.Equal([1, 3, 1], root.SubtreeActivityCounts);
        Assert.Equal([0, 3, 1], collapsed.GetNodes().Single(node => node.Symbol == "Y::m").SubtreeActivityCounts);
    }

    [Fact]
    public void Call_Activity_Is_Built_Once_And_Only_When_A_Node_Asks_For_Its_Counts()
    {
        var tree = new CallTree();

        var x = tree.AddCall(tree.Root, Frame("X", 100), 1);

        var y = tree.AddCall(x, Frame("Y", 200), 4);

        var builds = 0;

        tree.SetCallActivity(() =>
        {
            builds++;

            return new Dictionary<CallTreeNode, int[]>(ReferenceEqualityComparer.Instance) { [x] = [1, 0], [y] = [0, 3] };
        });

        tree.ComputeActivity(2);

        Assert.Equal(0, builds);
        Assert.Equal([1, 3], x.SubtreeActivityCounts);
        Assert.Equal([0, 3], y.SubtreeActivityCounts);
        Assert.Equal(1, builds);
    }

    private static string Render(CallTree tree)
        => string.Concat(tree.Root.Children.OrderBy(c => c.Order).Select(CallTree.Render));

    private static void Path(CallTree tree, params CallFrame[] frames)
    {
        var node = tree.Root;

        for (var index = 0; index < frames.Length; index++)
        {
            node = tree.AddCall(node, frames[index], index == frames.Length - 1 ? 1 : 0);
        }
    }

    private static CallFrame Frame(string name, uint rva) => new()
    {
        Module = new("sqllang"),
        Address = rva,
        Rva = rva,
        Function = new FrameFunction { ClassName = name, MethodName = "m" },
    };
}
