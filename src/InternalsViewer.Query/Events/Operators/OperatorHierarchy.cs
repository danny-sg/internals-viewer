using InternalsViewer.Query.Parsing.Plans;

namespace InternalsViewer.Query.Events.Operators;

/// <summary>
/// The plan's operators as a walkable tree, with each operator's subtree and the events in it
/// </summary>
/// <remarks>
/// <see cref="ExecutionOperatorEvent"/> knows only its parent's node id, so everything that needs to walk DOWN the plan
/// — an operator's descendants, the events in its subtree, the roots — has to invert that first. Inverting it here keeps
/// the walk, and the guard against a plan that turns out not to be a tree, in one place. It was previously written once
/// in <see cref="CallStack.OperatorCallStackMatcher"/> and again in the call-stack view, cycle guard and all, which also
/// left the view deciding what an operator's scope is.
/// </remarks>
public sealed class OperatorHierarchy
{
    private readonly ILookup<(short PlanHandleId, int NodeId), ExecutionOperatorEvent> _childrenByParent;

    private readonly Dictionary<PlanNodeIdentifier, List<ExecutionOperatorEvent>> _descendants;

    private readonly Dictionary<PlanNodeIdentifier, ExecutionOperatorEvent> _byNode;

    private OperatorHierarchy(List<ExecutionOperatorEvent> operators)
    {
        Operators = operators;

        _childrenByParent = operators
            .Where(o => o.ParentNodeId is not null)
            .ToLookup(o => (o.PlanNodeIdentifier!.PlanHandleId, o.ParentNodeId!.Value));

        _byNode = operators
            .GroupBy(o => o.PlanNodeIdentifier!)
            .ToDictionary(operatorEvent => operatorEvent.Key, operatorEvent => operatorEvent.First());

        _descendants = operators
            .GroupBy(o => o.PlanNodeIdentifier!)
            .ToDictionary(operatorEvent => operatorEvent.Key, operatorEvent => Collect(operatorEvent.First()));

        var nodeIds = operators.Select(o => o.PlanNodeIdentifier!.NodeId).ToHashSet();

        Roots = operators.Where(o => o.ParentNodeId is null || !nodeIds.Contains(o.ParentNodeId.Value))
                         .OrderBy(o => o.PlanNodeIdentifier!.NodeId)
                         .ToList();
    }

    /// <summary>
    /// Every operator the plan matched to a node
    /// </summary>
    public IReadOnlyList<ExecutionOperatorEvent> Operators { get; }

    /// <summary>
    /// The operators nothing here is the parent of — the statement node, or the top operators when it was not built
    /// </summary>
    public IReadOnlyList<ExecutionOperatorEvent> Roots { get; }

    public static OperatorHierarchy Build(IEnumerable<EngineEvent> events)
        => new([.. events.OfType<ExecutionOperatorEvent>().Where(o => o.PlanNodeIdentifier is not null)]);

    public IEnumerable<ExecutionOperatorEvent> Children(ExecutionOperatorEvent operatorEvent)
        => _childrenByParent[(operatorEvent.PlanNodeIdentifier!.PlanHandleId, operatorEvent.PlanNodeIdentifier.NodeId)];

    /// <summary>
    /// The operator that drives this one, or null for a root or when the parent was not captured
    /// </summary>
    public ExecutionOperatorEvent? Parent(ExecutionOperatorEvent operatorEvent)
        => operatorEvent.ParentNodeId is { } parent
            ? _byNode.GetValueOrDefault(new PlanNodeIdentifier
              {
                  PlanHandleId = operatorEvent.PlanNodeIdentifier!.PlanHandleId,
                  NodeId = parent,
              })
            : null;

    public IReadOnlyList<ExecutionOperatorEvent> Descendants(ExecutionOperatorEvent operatorEvent)
        => _descendants.GetValueOrDefault(operatorEvent.PlanNodeIdentifier!) ?? [];

    /// <summary>
    /// The plan nodes an operator covers: itself and everything beneath it
    /// </summary>
    public HashSet<PlanNodeIdentifier> Subtree(ExecutionOperatorEvent operatorEvent)
        => [operatorEvent.PlanNodeIdentifier!, .. Descendants(operatorEvent).Select(d => d.PlanNodeIdentifier!)];

    /// <summary>
    /// The events an operator's work covers: its whole subtree's, expanded to everything they own
    /// </summary>
    /// <remarks>
    /// The subtree rather than the operator's own events, because only the data-access leaves emit anything — scoping a
    /// Stream Aggregate to its own events would leave it blank. Expanded because a group carries no call stack (its
    /// members do) and a folded End is the release path of the Begin that survived.
    /// </remarks>
    public HashSet<EngineEvent> ScopeOf(ExecutionOperatorEvent operatorEvent, IEnumerable<EngineEvent> events)
    {
        var subtree = Subtree(operatorEvent);

        return events.Where(e => e is not ExecutionOperatorEvent
                                 && e.PlanNodeIdentifier is { } id
                                 && subtree.Contains(id))
                     .ExpandOwned();
    }

    private List<ExecutionOperatorEvent> Collect(ExecutionOperatorEvent operatorEvent)
    {
        var collected = new List<ExecutionOperatorEvent>();

        var visited = new HashSet<PlanNodeIdentifier> { operatorEvent.PlanNodeIdentifier! };

        var pending = new Stack<ExecutionOperatorEvent>();

        pending.Push(operatorEvent);

        while (pending.Count > 0)
        {
            foreach (var child in Children(pending.Pop()))
            {
                // Visited-guarded rather than trusting the plan to be a tree: a cycle here would hang the whole run.
                if (visited.Add(child.PlanNodeIdentifier!))
                {
                    collected.Add(child);

                    pending.Push(child);
                }
            }
        }

        return collected;
    }
}
