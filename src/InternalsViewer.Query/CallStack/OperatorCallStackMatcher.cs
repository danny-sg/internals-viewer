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
/// Three signals, because none of them covers the others' ground:
///
/// <list type="bullet">
/// <item>The mapping file names the frame's operator. It is the only thing that separates a CHAIN — a Stream Aggregate
/// over an Index Scan owns exactly the scan's events and nothing else, so no amount of event data tells the two
/// apart.</item>
/// <item>The plan's SHAPE orders those names. A name belongs to the CLASS and cannot tell instances of it apart: asked
/// independently which frames match "Hash Match", each of six nested hash joins claims all six runs. The plan already
/// says which order they appear in on any stack, so a run is fixed by its POSITION in the leaf's chain of ancestors
/// instead. The names never needed to be unique, only correctly ordered.</item>
/// <item>Ownership needs no names at all. It separates a BRANCH — where an operator has siblings, its events are its
/// own, and the frame below which only its subtree's events appear is where it began. This is what the names are worst
/// at: a class the file does not cover costs the parent its trim as well as the child its segment.</item>
/// </list>
///
/// Alignment is tried first and ownership fills its gaps, though the reverse is tempting. It was tried, and it loses on
/// the general case: "where this node's work branches off" is not "where this node was entered" — a nested loop
/// re-enters its inner side once per outer row, so the lookup's work branches away at every row-release and lock the
/// loop drives, and ownership honestly reports all dozen of them. One of them is the entry; the rule cannot say which.
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

        var alignedByNode = AlignedEntryFrames(hierarchy, leavesByNode, nodesByFrame);

        foreach (var operatorEvent in hierarchy.Operators)
        {
            operatorEvent.EntryFrames = alignedByNode.GetValueOrDefault(operatorEvent.PlanNodeIdentifier!) is
                                        { Count: > 0 } aligned
                ? aligned
                : OwnedEntryFrames(nodesByFrame, allNodes, hierarchy.Subtree(operatorEvent));
        }

        // Exits second: an operator's segment ends where its descendants' segments start, so every entry must be known.
        foreach (var operatorEvent in hierarchy.Operators)
        {
            operatorEvent.ExitFrames = ExitFrames(operatorEvent, hierarchy);
        }
    }

    /// <summary>
    /// Every operator's entry frames, found by walking each leaf's stack once against the plan chain above it
    /// </summary>
    /// <remarks>
    /// Inverted from the obvious loop: a leaf is walked once and hands each run it meets to the next operator on its
    /// chain, rather than each operator being asked which frames carry its name. Asking per operator cannot work — the
    /// name is the class's, so an ancestor is handed the nearest same-named DESCENDANT's run and six nested hash joins
    /// dissolve into each other.
    /// </remarks>
    private static Dictionary<PlanNodeIdentifier, List<CallStackNode>> AlignedEntryFrames(
        OperatorHierarchy hierarchy,
        Dictionary<PlanNodeIdentifier, List<CallStackNode>> leavesByNode,
        Dictionary<CallStackNode, HashSet<PlanNodeIdentifier>> nodesByFrame)
    {
        var entryFrames = new Dictionary<PlanNodeIdentifier, List<CallStackNode>>();

        var subtrees = hierarchy.Operators.ToDictionary(o => o.PlanNodeIdentifier!, hierarchy.Subtree);

        foreach (var (node, leaves) in leavesByNode)
        {
            var chain = Chain(hierarchy, node);

            if (chain.Count == 0)
            {
                continue;
            }

            foreach (var leaf in leaves)
            {
                Align(leaf, chain, subtrees, nodesByFrame, entryFrames);
            }
        }

        return entryFrames;
    }

    /// <summary>
    /// Which operator at or above <paramref name="from"/> on the chain a frame belongs to, or -1 for none of them
    /// </summary>
    /// <remarks>
    /// The name alone cannot answer this — it is the CLASS's, and every one of six nested hash joins is CQScanHash — so
    /// the chain's ORDER is what fixes a run, and this is where the walk decides it has reached the next operator.
    ///
    /// Position alone is not enough either, because the walk does not always start in step. An event is attributed to a
    /// plan node whose operator may never appear on its stack at all: the Open cascade constructs and opens every
    /// iterator in the plan before any of them returns a row, so a memory grant taken there is attributed to some deep
    /// node while its stack shows only the outermost join's Open. Skipping on names alone, that leaf hands node 1's
    /// Open to node 17 — the first chain entry whose name fits — and the outermost join's segment lands inside the
    /// innermost's. Six hash joins claiming one frame is the whole reported bug, arrived at by a second route.
    ///
    /// So ownership arbitrates: node 1's Open has the whole plan beneath it, node 17's subtree is three nodes, and the
    /// events say it cannot be node 17's however well the name fits. The search passes over it and finds node 1, which
    /// is on the same chain and which it fits exactly.
    ///
    /// The name-only pass behind it is not a hedge, it is the merged-sibling case: two Index Seeks under one join run
    /// identical code and collapse onto ONE tree node, so its events are both seeks' and it is a subset of neither's
    /// subtree. Nothing can separate them — not names, not ownership, not the plan — and the honest answer is the one
    /// the ownership rule cannot give, that the frame is both of theirs. Requiring the strict rule would take their
    /// entry away entirely and gain nothing. It is only reached when NO operator on the chain fits exactly, so it can
    /// never overrule a frame that has a proper owner.
    /// </remarks>
    private static int Position(CallStackNode frame,
                                List<ExecutionOperatorEvent> chain,
                                int from,
                                Dictionary<PlanNodeIdentifier, HashSet<PlanNodeIdentifier>> subtrees,
                                Dictionary<CallStackNode, HashSet<PlanNodeIdentifier>> nodesByFrame)
    {
        var named = -1;

        for (var index = from; index < chain.Count; index++)
        {
            if (!frame.IsEntryFrameFor(chain[index].Name))
            {
                continue;
            }

            if (Owns(frame, chain[index], subtrees, nodesByFrame))
            {
                return index;
            }

            named = named < 0 ? index : named;
        }

        return named;
    }

    /// <summary>
    /// Whether the events witness this frame as the operator's own — nothing outside its plan subtree ran beneath it
    /// </summary>
    /// <remarks>
    /// Only frames the ownership index knows are constrained. It excludes the statement's preamble (see
    /// <see cref="RanInsideAnOperator"/>), and an unwitnessed frame is left to the name rather than judged on no
    /// evidence.
    /// </remarks>
    private static bool Owns(CallStackNode frame,
                             ExecutionOperatorEvent operatorEvent,
                             Dictionary<PlanNodeIdentifier, HashSet<PlanNodeIdentifier>> subtrees,
                             Dictionary<CallStackNode, HashSet<PlanNodeIdentifier>> nodesByFrame)
        => !nodesByFrame.TryGetValue(frame, out var beneath)
           || !subtrees.TryGetValue(operatorEvent.PlanNodeIdentifier!, out var subtree)
           || beneath.IsSubsetOf(subtree);

    /// <summary>
    /// Consumes one leaf's stack against its plan chain, assigning each run of frames to the operator it belongs to
    /// </summary>
    /// <remarks>
    /// The chain is the order the operators must appear in walking outward, so a run's place in it — not its name —
    /// decides whose it is; <see cref="Position"/> is what settles that. Each run is kept at its OUTERMOST frame: an
    /// operator is rarely one frame (a hash join spans Open, Iterate, ConsumeBuild, ReadRow, all its own) and stopping
    /// at the innermost would enter it at ReadRow and leave the build it was called from outside the segment. The run
    /// must be CONTIGUOUS, which is what stops a nested loop's outer repeat — its frame reappears above its inner
    /// side's — from swallowing the whole recursion.
    /// </remarks>
    private static void Align(CallStackNode leaf,
                              List<ExecutionOperatorEvent> chain,
                              Dictionary<PlanNodeIdentifier, HashSet<PlanNodeIdentifier>> subtrees,
                              Dictionary<CallStackNode, HashSet<PlanNodeIdentifier>> nodesByFrame,
                              Dictionary<PlanNodeIdentifier, List<CallStackNode>> entryFrames)
    {
        var chainIndex = 0;

        CallStackNode? run = null;

        foreach (var frame in leaf.Ancestors())
        {
            // Skipping ahead is what lets an operator with no frames of its own — an inlined Compute Scalar — be passed
            // over instead of desynchronising the chain and losing everything above it. A frame belonging to no
            // operator left on the chain finds no position and is ignored, which is what the outer repeat of a
            // recursive Nested Loops needs.
            var position = Position(frame, chain, chainIndex, subtrees, nodesByFrame);

            if (run is not null && position == chainIndex)
            {
                run = frame;

                continue;
            }

            if (run is not null)
            {
                Assign(entryFrames, chain[chainIndex], run);

                run = null;

                chainIndex++;

                position = Position(frame, chain, chainIndex, subtrees, nodesByFrame);
            }

            if (position >= 0)
            {
                chainIndex = position;

                run = frame;
            }
        }

        if (run is not null)
        {
            Assign(entryFrames, chain[chainIndex], run);
        }
    }

    /// <summary>
    /// The operators from a plan node out to the root, in the order their frames appear walking up a stack
    /// </summary>
    private static List<ExecutionOperatorEvent> Chain(OperatorHierarchy hierarchy, PlanNodeIdentifier node)
    {
        var chain = new List<ExecutionOperatorEvent>();

        // Visited-guarded rather than trusting the plan to be a tree, as OperatorHierarchy.Collect is: a cycle here
        // would hang the whole run.
        var visited = new HashSet<PlanNodeIdentifier>();

        for (var operatorEvent = hierarchy.At(node);
             operatorEvent is not null && visited.Add(operatorEvent.PlanNodeIdentifier!);
             operatorEvent = hierarchy.Parent(operatorEvent))
        {
            chain.Add(operatorEvent);
        }

        return chain;
    }

    private static void Assign(Dictionary<PlanNodeIdentifier, List<CallStackNode>> entryFrames,
                               ExecutionOperatorEvent operatorEvent,
                               CallStackNode entry)
    {
        var frames = Bucket(entryFrames, operatorEvent.PlanNodeIdentifier!);

        if (!frames.Contains(entry))
        {
            frames.Add(entry);
        }
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

            // Only the ownership index is filtered. The aligned walk needs no protection from the preamble — a leaf out
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
