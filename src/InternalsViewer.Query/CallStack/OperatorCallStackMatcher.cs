using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Parsing.Plans;

namespace InternalsViewer.Query.CallStack;

/// <summary>
/// Finds the call-stack frames bounding each plan operator's execution, filling
/// <see cref="ExecutionOperatorEvent.EntryFrames"/> and <see cref="ExecutionOperatorEvent.ExitFrames"/>
/// </summary>
/// <remarks>
/// Operators emit no events of their own — only the data-access leaves do — so an operator's frames cannot be found from
/// its own events, and most operators have none at all. They are found from the leaves of its plan SUBTREE instead: a
/// Stream Aggregate appears on the stacks of the reads its Index Scan issued, and walking up from those reaches it.
///
/// Two signals, because neither covers the other's ground:
///
/// <list type="bullet">
/// <item>The mapping file names the frame's operator. It is the only thing that separates a CHAIN — a Stream Aggregate
/// over an Index Scan owns exactly the scan's events and nothing else, so no amount of event data tells the two
/// apart.</item>
/// <item>Ownership needs no names. It separates a BRANCH — where an operator has siblings, its events are its own, and
/// the frame below which only its subtree's events appear is where it began. This is what the names are worst at: one
/// iterator class serves operators the plan names unrelatedly (CQScanRange is the seek AND the Key Lookup), and a
/// missing name costs the parent its trim as well as the child its segment.</item>
/// </list>
///
/// The mapping is tried first and ownership fills its gaps, though the reverse is tempting: a name belongs to the CLASS
/// and cannot tell instances of it apart, so "Index Seek" on CQScanRangeNew hands the seek the Key Lookup's copy of that
/// frame as well as its own, where ownership separates the two exactly.
///
/// It was tried, and it loses on the general case. "Where this node's work branches off" is not "where this node was
/// entered" — a nested loop re-enters its inner side once per outer row, so the lookup's work branches away at every
/// row-release and lock the loop drives, and ownership honestly reports all dozen of them. One of them is the entry; the
/// rule cannot say which. Naming has no such trouble, and where it is wrong it is wrong in a bounded way.
///
/// Measured, not assumed: OperatorScopeIntegrationTests dumps both answers per operator against real queries.
///
/// Runs after <see cref="CallStackTree.CollapseToFunctions"/>, needing both resolved symbols (for the mapping match) and
/// the final function-keyed nodes (the ones the events point at).
/// </remarks>
public static class OperatorCallStackMatcher
{
    public static void Match(IReadOnlyList<EngineEvent> events)
    {
        var hierarchy = OperatorHierarchy.Build(events);

        var leavesByNode = new Dictionary<PlanNodeIdentifier, List<CallStackNode>>();

        var nodesByFrame = new Dictionary<CallStackNode, HashSet<PlanNodeIdentifier>>();

        var allNodes = new HashSet<PlanNodeIdentifier>();

        foreach (var engineEvent in events)
        {
            if (engineEvent is not ExecutionOperatorEvent && engineEvent.PlanNodeIdentifier is { } id)
            {
                IndexEvent(engineEvent, id, leavesByNode, nodesByFrame, allNodes);
            }
        }

        foreach (var operatorEvent in hierarchy.Operators)
        {
            var subtree = hierarchy.Subtree(operatorEvent);

            operatorEvent.EntryFrames = MappedEntryFrames(operatorEvent, subtree, leavesByNode) is { Count: > 0 } mapped
                ? mapped
                : OwnedEntryFrames(nodesByFrame, allNodes, subtree);
        }

        // Exits second: an operator's segment ends where its descendants' segments start, so every entry must be known.
        foreach (var operatorEvent in hierarchy.Operators)
        {
            operatorEvent.ExitFrames = ExitFrames(operatorEvent, hierarchy);
        }
    }

    /// <summary>
    /// The frames the mapping file names as this operator's, found by walking up from its subtree's events
    /// </summary>
    private static List<CallStackNode> MappedEntryFrames(ExecutionOperatorEvent operatorEvent,
                                                         HashSet<PlanNodeIdentifier> subtree,
                                                         Dictionary<PlanNodeIdentifier, 
                                                         List<CallStackNode>> leavesByNode)
    {
        // Name is the physical operator - OperatorEventBuilder sets it from PlanNode.PhysicalOperator.
        var physicalOperator = operatorEvent.Name;

        var entryFrames = new List<CallStackNode>();

        foreach (var node in subtree)
        {
            if (!leavesByNode.TryGetValue(node, out var leaves))
            {
                continue;
            }

            foreach (var leaf in leaves)
            {
                if (EntryFrame(leaf, physicalOperator) is { } entry && !entryFrames.Contains(entry))
                {
                    entryFrames.Add(entry);
                }
            }
        }

        return entryFrames;
    }

    /// <summary>
    /// The frames below which only this operator's subtree ran, found from the events alone
    /// </summary>
    /// <remarks>
    /// A frame qualifies when everything beneath it belongs to the operator's plan subtree AND its caller's does not:
    /// that second half is the whole idea. It says the frame is where this operator's work branches away from its
    /// siblings', which is the only thing the events can actually witness.
    ///
    /// Without it, a chain would resolve to nonsense. When a Stream Aggregate's only events are its Index Scan's, every
    /// frame from the thread start down owns exactly that set, so "everything beneath is mine" is true of all of them
    /// and the scan would claim the whole tree. Requiring the caller to own something else means no frame qualifies and
    /// nothing is claimed — correct, because in a chain there genuinely is no evidence.
    /// </remarks>
    private static List<CallStackNode> OwnedEntryFrames(Dictionary<CallStackNode, HashSet<PlanNodeIdentifier>> nodesByFrame,
                                                        HashSet<PlanNodeIdentifier> allNodes,
                                                        HashSet<PlanNodeIdentifier> subtree)
    {
        var entryFrames = new List<CallStackNode>();

        foreach (var (frame, nodes) in nodesByFrame)
        {
            if (!nodes.IsSubsetOf(subtree))
            {
                continue;
            }

            // Above a root frame is the capture itself, which ran everything — so a root frame is an entry only when the
            // query did work outside this operator for it to be distinguishable from.
            var caller = frame.Parent is { Frame: not null } parent ? nodesByFrame[parent] : allNodes;

            if (!caller.IsSubsetOf(subtree))
            {
                entryFrames.Add(frame);
            }
        }

        return entryFrames;
    }

    private static List<CallStackNode> ExitFrames(ExecutionOperatorEvent operatorEvent, OperatorHierarchy hierarchy)
    {
        var exitFrames = new List<CallStackNode>();

        foreach (var descendant in hierarchy.Descendants(operatorEvent))
        {
            foreach (var entry in descendant.EntryFrames)
            {
                if (!operatorEvent.EntryFrames.Contains(entry) && !exitFrames.Contains(entry))
                {
                    exitFrames.Add(entry);
                }
            }
        }

        return exitFrames;
    }

    /// <summary>
    /// Records an event's plan node against its leaf and against every frame that led to it
    /// </summary>
    private static void IndexEvent(EngineEvent engineEvent,
                                   PlanNodeIdentifier id,
                                   Dictionary<PlanNodeIdentifier, List<CallStackNode>> leavesByNode,
                                   Dictionary<CallStackNode, HashSet<PlanNodeIdentifier>> nodesByFrame,
                                   HashSet<PlanNodeIdentifier> allNodes)
    {
        // The owner's node, not the owned event's: a group's members carry the frames but the group is what was matched
        // to the plan, and a folded End is the release path of the Begin that survived.
        foreach (var owned in engineEvent.SelfAndOwned())
        {
            if (owned.CallStack is not { } leaf)
            {
                continue;
            }

            var leaves = Bucket(leavesByNode, id);

            if (!leaves.Contains(leaf))
            {
                leaves.Add(leaf);
            }

            // Only the ownership index is filtered. The mapped walk needs no protection from the preamble — a leaf out
            // there reaches no named frame and contributes nothing — whereas ownership reads these frames as the node's
            // own and offers them as entries.
            if (!RanInsideAnOperator(leaf))
            {
                continue;
            }

            // Counted only once it has frames. allNodes stands in for what the capture as a whole ran, so it is compared
            // against the frames' node sets — and a node that captured no stack (an operator with only a thread profile)
            // would inflate it without appearing in any of them, making every root frame look like a branch point.
            allNodes.Add(id);

            foreach (var frame in leaf.Ancestors())
            {
                if (!nodesByFrame.TryGetValue(frame, out var nodes))
                {
                    nodes = [];

                    nodesByFrame[frame] = nodes;
                }

                nodes.Add(id);
            }
        }
    }

    /// <summary>
    /// Where an operator was entered on the way to a leaf: the outermost frame of the first run of its own frames
    /// </summary>
    /// <remarks>
    /// An operator is rarely one frame. A hash join spans CQScanHash::Open, Iterate, ConsumeBuild, ReadRow — all of them
    /// its own, all matched by the same rule — so stopping at the first one found walking up from a leaf enters it at
    /// ReadRow and leaves the build it was called from outside the segment.
    ///
    /// The run has to be contiguous, and that is what separates this from simply taking the outermost match. When a
    /// Nested Loops drives its inner side, its frame appears again further up with the inner operator's frames in
    /// between; the run breaks there, so the operator is entered at the copy the leaf actually came out of rather than
    /// at the outer repeat, which would swallow the whole recursion.
    /// </remarks>
    private static CallStackNode? EntryFrame(CallStackNode leaf, string physicalOperator)
    {
        CallStackNode? entry = null;

        foreach (var frame in leaf.Ancestors())
        {
            if (frame.IsEntryFrameFor(physicalOperator))
            {
                entry = frame;
            }
            else if (entry is not null)
            {
                break;
            }
        }

        return entry;
    }

    /// <summary>
    /// Whether an event happened inside the plan's execution, rather than in the statement's preamble around it
    /// </summary>
    /// <remarks>
    /// Being matched to a node does not mean an event happened inside that node's work. The scan's Object/IS lock is
    /// taken while the statement validates its schema (CXStmtQuery::XretSchemaChanged) and released as it cleans up
    /// (CXStmtQuery::FinishNormalImp) — correctly the scan's lock, but nowhere near the scan. Left in, those frames read
    /// as "only this node ran below me" and offer themselves as entries alongside the operator's real one.
    ///
    /// The plan's execution always runs through an iterator; the preamble around it never does. That is a fact about
    /// the shape of the stack rather than about any name in it, so it does not decay as the mapping file does.
    ///
    /// An iterator's CONSTRUCTOR does not count. CQueryScan::Setup builds the whole plan before any of it runs, so a
    /// wait taken while a hash iterator lays out its partitions is under CQScanHash::CQScanHash — an iterator frame, but
    /// construction, not execution.
    /// </remarks>
    private static bool RanInsideAnOperator(CallStackNode leaf)
        => leaf.Ancestors().Any(IsExecutingIterator);

    private static bool IsExecutingIterator(CallStackNode frame)
        => frame.Frame?.Resolved?.SymbolCategory is SymbolCategory.QueryOperator or SymbolCategory.PhysicalOperator
           && !frame.IsConstructor;



    private static List<CallStackNode> Bucket(Dictionary<PlanNodeIdentifier, List<CallStackNode>> map,
                                              PlanNodeIdentifier key)
    {
        if (!map.TryGetValue(key, out var leaves))
        {
            leaves = [];

            map[key] = leaves;
        }

        return leaves;
    }
}
