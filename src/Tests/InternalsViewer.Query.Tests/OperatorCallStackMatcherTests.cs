using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class OperatorCallStackMatcherTests
{
    [Fact]
    public void An_Operators_Entry_Frame_Is_Found_By_Walking_Up_From_Its_Events()
    {
        var tree = new CallStackTree();

        var read = Event(node: 1);

        tree.Add([Frame("BufRead"), Operator("Seek", "Index Seek"), Frame("Thread")], read);

        var seek = Operator(node: 1, physicalOperator: "Index Seek");

        Match(tree, [seek, read]);

        Assert.Equal("Seek::m", Assert.Single(seek.EntryFrames).Symbol);
    }

    [Fact]
    public void An_Operator_Spanning_Several_Frames_Is_Entered_At_The_Outermost_Of_Them()
    {
        // A hash join is Open -> Iterate -> ConsumeBuild -> ReadRow, every frame its own and matched by the same rule.
        // Stopping at the first found walking up from a leaf enters it at ReadRow and leaves the build that called it
        // outside the segment — which is exactly the "Hash Match loses its Open/Iterate/ConsumeBuild" report.
        var tree = new CallStackTree();

        var read = Event(node: 1);

        tree.Add([Frame("BufRead", 10), Operator("Hash", "Hash Match", rva: 20), Operator("Hash", "Hash Match", rva: 30),
                  Operator("Hash", "Hash Match", rva: 40), Frame("Startup", 50)], read);

        var hash = Operator(node: 1, physicalOperator: "Hash Match");

        Match(tree, [hash, read]);

        // The outermost Hash frame — the one Startup called — not the innermost the read came out of.
        var entry = Assert.Single(hash.EntryFrames);

        Assert.Equal("Startup::m", entry.Parent?.Symbol);
    }

    [Fact]
    public void A_Recursive_Operator_Is_Entered_At_The_Copy_Its_Event_Came_From()
    {
        // The run of an operator's own frames has to be CONTIGUOUS. A Nested Loops drives its inner side, so its frame
        // appears again further up with the inner operator's frames in between; entering at that outer repeat would
        // hand it the whole recursion. The break in the run is what stops it.
        var tree = new CallStackTree();

        var read = Event(node: 1);

        tree.Add([Frame("BufRead"), Operator("Join", "Nested Loops", rva: 20), Frame("Inner"),
                  Operator("Join", "Nested Loops", rva: 40), Frame("Thread")], read);

        var join = Operator(node: 1, physicalOperator: "Nested Loops");

        Match(tree, [join, read]);

        var entry = Assert.Single(join.EntryFrames);

        Assert.Equal("Join::m", entry.Symbol);
        Assert.Equal("Inner::m", entry.Parent?.Symbol);
    }

    [Fact]
    public void Nested_Operators_Of_One_Kind_Each_Take_Their_Own_Frame()
    {
        // Six nested Hash Matches all run CQScanHash at the same RVAs, so "which frames match my name?" has no single
        // answer — asked independently, the outer join is handed the inner join's run by the leaf beneath it and every
        // segment dissolves into every other. The plan's shape is the only thing left that separates them: walking up
        // from the leaf its operators appear in chain order, so each run is fixed by its POSITION.
        var tree = new CallStackTree();

        var read = Event(node: 3);

        tree.Add([Frame("BufRead", 10), Operator("Seek", "Index Seek", 20), Operator("Hash", "Hash Match", 30),
                  Frame("Profile", 40), Operator("Hash", "Hash Match", 50), Frame("Thread", 60)], read);

        var outerHash = Operator(node: 1, physicalOperator: "Hash Match");

        var innerHash = Operator(node: 2, physicalOperator: "Hash Match", parent: 1);

        var seek = Operator(node: 3, physicalOperator: "Index Seek", parent: 2);

        Match(tree, [outerHash, innerHash, seek, read]);

        var inner = Assert.Single(innerHash.EntryFrames);

        var outer = Assert.Single(outerHash.EntryFrames);

        // Same symbol, different frames: the inner join is the one the seek came out of, the outer the one Thread called.
        Assert.Equal("Hash::m", inner.Symbol);
        Assert.Equal("Profile::m", inner.Parent?.Symbol);

        Assert.Equal("Hash::m", outer.Symbol);
        Assert.Equal("Thread::m", outer.Parent?.Symbol);
    }

    [Fact]
    public void Two_Operators_Of_One_Kind_Sharing_A_Merged_Frame_Both_Resolve_To_It()
    {
        // Two Index Seeks under a join run identical code at identical addresses — they differ only by `this`, so the
        // function-keyed collapse merges them onto one node and no amount of frame data separates them. Working up from
        // each operator's OWN events sidesteps that: both simply resolve to the shared frame, which is honest.
        var tree = new CallStackTree();

        var first = Event(node: 1);

        var second = Event(node: 2);

        tree.Add([Frame("BufRead"), Operator("Seek", "Index Seek"), Frame("Thread")], first);
        tree.Add([Frame("BufRead"), Operator("Seek", "Index Seek"), Frame("Thread")], second);

        var firstSeek = Operator(node: 1, physicalOperator: "Index Seek");

        var secondSeek = Operator(node: 2, physicalOperator: "Index Seek");

        Match(tree, [firstSeek, secondSeek, first, second]);

        Assert.Same(Assert.Single(firstSeek.EntryFrames), Assert.Single(secondSeek.EntryFrames));
    }

    [Fact]
    public void An_Operator_With_No_Events_Of_Its_Own_Is_Found_On_Its_Childs_Stack()
    {
        // The relational operators emit nothing — only the data-access leaves do. A Stream Aggregate has no events at
        // all, so its frame can only be found by walking up from the reads its Index Scan issued. Looking at an
        // operator's OWN events finds nothing for every operator above a leaf, which is most of the plan.
        var tree = new CallStackTree();

        var read = Event(node: 2);

        tree.Add([Frame("BufRead"), Operator("Seek", "Index Seek"), Operator("Agg", "Stream Aggregate"), Frame("Thread")],
                 read);

        var aggregate = Operator(node: 1, physicalOperator: "Stream Aggregate");

        var seek = Operator(node: 2, physicalOperator: "Index Seek", parent: 1);

        Match(tree, [aggregate, seek, read]);

        Assert.Equal("Agg::m", Assert.Single(aggregate.EntryFrames).Symbol);
        Assert.Equal("Seek::m", Assert.Single(seek.EntryFrames).Symbol);
    }

    [Fact]
    public void An_Operators_Segment_Ends_Where_Its_Childrens_Begin()
    {
        // The child's entry is the parent's exit. It has to be stated: with no events of its own the aggregate has
        // nothing to derive the bottom of its segment from, so without this it would swallow the seek's whole subtree.
        var tree = new CallStackTree();

        var read = Event(node: 2);

        tree.Add([Frame("BufRead"), Operator("Seek", "Index Seek"), Operator("Agg", "Stream Aggregate"), Frame("Thread")],
                 read);

        var aggregate = Operator(node: 1, physicalOperator: "Stream Aggregate");

        var seek = Operator(node: 2, physicalOperator: "Index Seek", parent: 1);

        Match(tree, [aggregate, seek, read]);

        Assert.Equal("Seek::m", Assert.Single(aggregate.ExitFrames).Symbol);

        // The leaf operator has nothing below it, so its segment runs all the way down to its events.
        Assert.Empty(seek.ExitFrames);
    }

    [Fact]
    public void An_Operator_Does_Not_Exit_At_Its_Own_Entry_Frame()
    {
        // A recursive operator (or one the function-keyed collapse merged onto its descendant's node) shares its entry
        // with something below it. Taking that as an exit would end the segment at its own start — nothing at all.
        var tree = new CallStackTree();

        var read = Event(node: 2);

        tree.Add([Frame("BufRead"), Operator("Join", "Nested Loops"), Frame("Inner"),
                  Operator("Join", "Nested Loops", rva: 40)], read);

        var outer = Operator(node: 1, physicalOperator: "Nested Loops");

        var inner = Operator(node: 2, physicalOperator: "Nested Loops", parent: 1);

        Match(tree, [outer, inner, read]);

        Assert.DoesNotContain(Assert.Single(outer.EntryFrames), outer.ExitFrames);
    }

    [Fact]
    public void An_Unmapped_Operators_Entry_Is_Found_Where_Its_Events_Branch_From_Its_Siblings()
    {
        // Neither input's iterator class is named in the mappings — the case that kept biting, since one class serves
        // operators the plan names unrelatedly. Their events separate them anyway: below Outer everything belongs to
        // node 2 and below Inner to node 3, while the frame above holds both. No name involved.
        var tree = new CallStackTree();

        var outerRead = Event(node: 2);

        var innerRead = Event(node: 3);

        tree.Add([Frame("OuterRead", 10), Frame("Outer", 20), Frame("Helper", 30), Operator("Join", "Nested Loops", 40)],
                 outerRead);

        tree.Add([Frame("InnerRead", 11), Frame("Inner", 21), Frame("Helper", 30), Operator("Join", "Nested Loops", 40)],
                 innerRead);

        var join = Operator(node: 1, physicalOperator: "Nested Loops");

        var outer = Operator(node: 2, physicalOperator: "Index Seek", parent: 1);

        var inner = Operator(node: 3, physicalOperator: "Key Lookup", parent: 1);

        Match(tree, [join, outer, inner, outerRead, innerRead]);

        Assert.Equal("Outer::m", Assert.Single(outer.EntryFrames).Symbol);
        Assert.Equal("Inner::m", Assert.Single(inner.EntryFrames).Symbol);

        // And so the join can now trim both inputs — the thing a missing name costs the PARENT, not just the child.
        Assert.Equal(["Inner::m", "Outer::m"], join.ExitFrames.Select(f => f.Symbol).Order());
    }

    [Fact]
    public void A_Chain_Where_Only_The_Leaf_Has_Events_Falls_Back_To_The_Mapping()
    {
        // The aggregate's only events are its seek's, so every frame from the thread start down owns exactly that set
        // and ownership has nothing to separate them by. It must decline rather than hand the seek the whole tree, and
        // the name is the only signal left.
        var tree = new CallStackTree();

        var read = Event(node: 2);

        tree.Add([Frame("BufRead", 10), Operator("Seek", "Index Seek", 20), Operator("Agg", "Stream Aggregate", 30)],
                 read);

        var aggregate = Operator(node: 1, physicalOperator: "Stream Aggregate");

        var seek = Operator(node: 2, physicalOperator: "Index Seek", parent: 1);

        Match(tree, [aggregate, seek, read]);

        Assert.Equal("Agg::m", Assert.Single(aggregate.EntryFrames).Symbol);
        Assert.Equal("Seek::m", Assert.Single(seek.EntryFrames).Symbol);
    }

    [Fact]
    public void An_Unmapped_Chain_Claims_Nothing_Rather_Than_The_Whole_Tree()
    {
        // Same shape with no name to fall back on. Every frame owns the seek's events and only those, so "everything
        // below me is mine" is true of the thread start too — taking it would root the seek's segment at the capture.
        var tree = new CallStackTree();

        var read = Event(node: 2);

        tree.Add([Frame("BufRead", 10), Frame("Seek", 20), Frame("Agg", 30), Frame("Thread", 40)], read);

        var aggregate = Operator(node: 1, physicalOperator: "Stream Aggregate");

        var seek = Operator(node: 2, physicalOperator: "Index Seek", parent: 1);

        Match(tree, [aggregate, seek, read]);

        Assert.Empty(seek.EntryFrames);
        Assert.Empty(aggregate.EntryFrames);
    }

    [Fact]
    public void An_Iterators_Constructor_Is_Not_Its_Entry_Frame()
    {
        // CQueryScan::Setup builds every iterator before the plan runs, so a wait taken while one lays out its memory
        // sits under CQScanHash::CQScanHash — matched by the mappings, since their rules glob the function. Taking it
        // would root the segment in the plan's construction rather than its execution.
        var tree = new CallStackTree();

        var setupWait = Event(node: 1);

        tree.Add([Frame("Wait", 10), Operator("Agg", "Stream Aggregate", 20, constructor: true), Frame("Setup", 30)],
                 setupWait);

        var aggregate = Operator(node: 1, physicalOperator: "Stream Aggregate");

        Match(tree, [aggregate, setupWait]);

        Assert.Empty(aggregate.EntryFrames);
    }

    [Fact]
    public void An_Operator_Whose_Kind_Is_Not_On_Its_Stack_Gets_No_Entry_Frame()
    {
        // Inlined, or its iterator class is simply not in the mappings. Empty rather than a wrong guess — the caller
        // falls back to the full path and says so.
        var tree = new CallStackTree();

        var read = Event(node: 1);

        tree.Add([Frame("BufRead"), Operator("Seek", "Index Seek"), Frame("Thread")], read);

        var compute = Operator(node: 1, physicalOperator: "Compute Scalar");

        Match(tree, [compute, read]);

        Assert.Empty(compute.EntryFrames);
    }

    [Fact]
    public void A_Groups_Members_Contribute_Their_Frames()
    {
        // The group is what carries the PlanNodeIdentifier and reaches the event list, but it has no call stack of its
        // own — its raw members do. Without expanding it the operator would look as though it captured nothing.
        var tree = new CallStackTree();

        var member = Event(node: null);

        tree.Add([Frame("BufRead"), Operator("Seek", "Index Seek"), Frame("Thread")], member);

        var group = new ReadEventGroup { Name = "read", Events = [member], PlanNodeIdentifier = NodeId(1) };

        var seek = Operator(node: 1, physicalOperator: "Index Seek");

        Match(tree, [seek, group]);

        Assert.Equal("Seek::m", Assert.Single(seek.EntryFrames).Symbol);
    }

    [Fact]
    public void A_Folded_Ends_Frames_Contribute_Too()
    {
        // The release/completion path: IntervalCollapser drops the End, so only the Begin reaches the event list, but
        // the End's frames are still this operator's work.
        var tree = new CallStackTree();

        var begin = Event(node: 1);

        var end = Event(node: null);

        begin.FoldedFrom = end;

        tree.Add([Frame("Acquire"), Frame("Thread")], begin);
        tree.Add([Frame("Release"), Operator("Seek", "Index Seek"), Frame("Thread")], end);

        var seek = Operator(node: 1, physicalOperator: "Index Seek");

        Match(tree, [seek, begin]);

        Assert.Equal("Seek::m", Assert.Single(seek.EntryFrames).Symbol);
    }

    // As QueryRunner does: collapse first (which repoints the events onto the function-keyed nodes), then match — the
    // entry frames have to be nodes of the tree the events actually point at.
    private static void Match(CallStackTree tree, IReadOnlyList<EngineEvent> events)
    {
        tree.CollapseToFunctions();

        OperatorCallStackMatcher.Match(events);
    }

    private static int _sequence;

    private static EngineEvent Event(int? node) => new()
    {
        Name = "e",
        SequenceId = _sequence++,
        PlanNodeIdentifier = node is { } nodeId ? NodeId(nodeId) : null,
    };

    private static PlanNodeIdentifier NodeId(int nodeId) => new() { PlanHandleId = 1, NodeId = nodeId };

    private static ExecutionOperatorEvent Operator(int node, string physicalOperator, int? parent = null) => new()
    {
        Name = physicalOperator,
        OperatorDescription = physicalOperator,
        PlanNodeIdentifier = NodeId(node),
        ParentNodeId = parent,
    };

    private static CallstackFrame Frame(string name, uint rva = 10) => new()
    {
        Module = "sqlmin",
        Rva = rva,
        Resolved = new ResolvedCallstackFrame { ClassName = name, MethodName = "m" },
    };

    // Categorised as a query operator like the real iterator frames are — the matcher takes that as the marker of the
    // plan's execution, so a fixture without it looks like the statement's preamble and is skipped for ownership.
    private static CallstackFrame Operator(string name, string planOperator, uint rva = 20, bool constructor = false)
        => new()
    {
        Module = "sqlmin",
        Rva = rva,
        Resolved = new ResolvedCallstackFrame
        {
            ClassName = name,
            MethodName = constructor ? name : "m",
            SymbolCategory = SymbolCategory.QueryOperator,
            Iterator = planOperator,
            PlanOperator = [GlobPattern.Parse(planOperator)],
        },
    };
}
