using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Plans;
using InternalsViewer.UI.App.Helpers;

namespace InternalsViewer.UI.App.ViewModels.Query;

/// <summary>
/// Resolves the display colour for an engine event on demand, so the colour no longer has to be stored on
/// every event (a <see cref="Color"/> is 24 bytes each). The only state is the per-plan-node IO colour map,
/// which is built once when the provider is created (per query / event refresh) and reused for every lookup.
/// </summary>
public sealed class EventColourProvider
{
    private readonly Dictionary<PlanNodeIdentifier, Color> _ioOperatorNodes;

    public EventColourProvider(IReadOnlyList<ExecutionPlan> executionPlans)
    {
        var ioNodes = executionPlans
            .SelectMany(g => g.NodesById.Select(n => new PlanNodeIdentifier(g.PlanHandleId, n.Key)))
            .Distinct()
            .Select((s, i) => (Id: s, Index: i + 1))
            .ToList();

        _ioOperatorNodes = ioNodes.ToDictionary(
            k => k.Id,
            v => ColourHelpers.GetSeriesColour(ColourConstants.IoColour, v.Index, ioNodes.Count + 1));
    }

    /// <summary>The display colour for an event, computed on demand from its type, category and linked node.</summary>
    public Color GetColour(EngineEvent engineEvent)
    {
        // Operators are coloured by their category (data access / join / transformation / buffer).
        if (engineEvent is ExecutionOperatorEvent op)
        {
            return GetOperatorCategoryColour(op.Category);
        }

        // Transaction-log writes are red, and don't get tinted by any linked operator's object.
        if (engineEvent is TransactionLogEvent)
        {
            return ColourConstants.LogColour;
        }

        // Locks and waits can be linked to an operator's object (e.g. a SCH_S/Object lock) without
        // representing that operator's IO, so they keep their own colour rather than being tinted like the
        // data-access events.
        if (engineEvent is not LockEvent and not WaitEvent
            && engineEvent.PlanNodeIdentifier is { } id
            && _ioOperatorNodes.TryGetValue(id, out var colour))
        {
            return colour;
        }

        return GetEventColour(engineEvent);
    }

    // The statement (SELECT/INSERT/...) node is neutral grey, matching the timeline's statement bar,
    // rather than falling through to the transformation category colour.
    private static readonly Color StatementColour = Color.FromArgb(255, 130, 130, 130);

    /// <summary>The operator type colour for a plan node (e.g. data-access blue), at full alpha.</summary>
    internal static Color GetOperatorColour(PlanNode node)
        => node.IsStatement
            ? StatementColour
            : GetOperatorCategoryColour(OperatorClassifier.GetCategory(node));

    private static Color GetOperatorCategoryColour(OperatorCategory category)
    {
        return category switch
        {
            OperatorCategory.DataAccess => ColourConstants.DataAccessColour,
            OperatorCategory.Join => ColourConstants.JoinColour,
            OperatorCategory.Transformation => ColourConstants.TransformationColour,
            OperatorCategory.Buffer => ColourConstants.BufferColour,
            OperatorCategory.Modification => ColourConstants.LogColour,
            _ => Color.Gray
        };
    }

    private static Color GetEventColour(EngineEvent engineEvent)
    {
        return engineEvent switch
        {
            LockEvent => ColourConstants.LockColour,
            WaitEvent => ColourConstants.WaitColour,
            TransactionLogEvent => ColourConstants.LogColour,
            _ => Color.Gray
        };
    }
}
