using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Plans;
using InternalsViewer.UI.App.Helpers;

namespace InternalsViewer.UI.App.ViewModels.Query;

internal class EventColourProvider
{
    public static void SetEventColours(List<ExecutionPlan> executionPlans, List<EngineEvent> events)
    {
        //var ioOperatorNodes = executionPlans.SelectMany(v => v.NodesById.Values
        //        .Where(OperatorClassifier.IsDataAccess)
        //        .Select((s, i) => (v.PlanHandle, s.NodeId)))
        //    .ToDictionary(k => new PlanNodeIdentifier(k.PlanHandle, k.NodeId), v => ColourHelpers.GetSeriesColour(ColourConstants.IoColour,
        //        v.NodesById.Count,
        //        i)));

        var ioNodes = executionPlans
            .SelectMany(g => g.NodesById.Select(n => new PlanNodeIdentifier(g.PlanHandle, n.Key)))
            .Distinct()
            .Select((s, i) => (Id: s, Index: i + 1))
            .ToList();

        var ioOperatorNodes = ioNodes.ToDictionary(k => k.Id, 
            v => ColourHelpers.GetSeriesColour(ColourConstants.IoColour,
                v.Index,
                ioNodes.Count + 1));

        foreach (var engineEvent in events)
        {
            // Operators are coloured by their category (data access / join / transformation / buffer).
            if (engineEvent is ExecutionOperatorEvent op)
            {
                engineEvent.DisplayColour = GetOperatorCategoryColour(op.Category);
                continue;
            }

            // Transaction-log writes are red, and don't get tinted by any linked operator's object.
            if (engineEvent is TransactionLogEvent)
            {
                engineEvent.DisplayColour = ColourConstants.LogColour;
                continue;
            }

            // Locks and waits can be linked to an operator's object (e.g. a SCH_S/Object lock) without
            // representing that operator's IO, so they keep their own colour rather than being tinted
            // like the data-access events.
            if (engineEvent is not LockEvent and not WaitEvent &&
                engineEvent.PlanNodeIdentifier != null &&
                ioOperatorNodes.TryGetValue(engineEvent.PlanNodeIdentifier, out var colour))
            {
                engineEvent.DisplayColour = colour;
                continue;
            }

            engineEvent.DisplayColour = GetEventColour(engineEvent);
        }
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

    private static Color GetColour(Color baseColour, int count, int i)
    {
        float hue = (float)i / count;

        return Color.FromArgb(baseColour.A, (int)(baseColour.R * hue), (int)(baseColour.G * hue), (int)(baseColour.B * hue));
    }
}