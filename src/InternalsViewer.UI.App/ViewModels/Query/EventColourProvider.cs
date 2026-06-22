using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using InternalsViewer.Replay.Events.EventTypes;
using InternalsViewer.Replay.Plans;
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

    private static Color GetEventColour(EngineEvent engineEvent)
    {
        return engineEvent switch
        {
            LockEvent => ColourConstants.LockColour,
            WaitEvent => ColourConstants.WaitColour,
            _ => Color.Gray
        };
    }

    private static Color GetColour(Color baseColour, int count, int i)
    {
        float hue = (float)i / count;

        return Color.FromArgb(baseColour.A, (int)(baseColour.R * hue), (int)(baseColour.G * hue), (int)(baseColour.B * hue));
    }
}