
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Events.Operators;

/// <summary>
/// Plan operators as a walkable tree
/// </summary>
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
    /// The operator at a plan node, or null when nothing here was matched to it
    /// </summary>
    public ExecutionOperatorEvent? At(PlanNodeIdentifier node) => _byNode.GetValueOrDefault(node);

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
