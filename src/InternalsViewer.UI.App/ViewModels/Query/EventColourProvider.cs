using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Plans;
using InternalsViewer.UI.App.Helpers;

namespace InternalsViewer.UI.App.ViewModels.Query;

public sealed class EventColourProvider
{
    private const double LatchDarkenFactor = 0.6;

    private readonly Dictionary<PlanNodeIdentifier, Color> _ioOperatorNodes;

    private readonly IReadOnlyDictionary<string, Color> _objectColours;

    public EventColourProvider(IReadOnlyList<ExecutionPlan> executionPlans,
                               IReadOnlyDictionary<string, Color>? objectColours = null)
    {
        var ioNodes = executionPlans
            .SelectMany(g => g.NodesById.Select(n => new PlanNodeIdentifier(g.PlanHandleId, n.Key)))
            .Distinct()
            .Select((s, i) => (Id: s, Index: i + 1))
            .ToList();

        _ioOperatorNodes = ioNodes.ToDictionary(
            k => k.Id,
            v => ColourHelpers.GetSeriesColour(ColourConstants.IoColour, v.Index, ioNodes.Count + 1));

        _objectColours = objectColours ?? new Dictionary<string, Color>();
    }

    public Color? GetObjectColour(string? objectName) =>
        !string.IsNullOrEmpty(objectName) && _objectColours.TryGetValue(objectName, out var colour)
            ? colour
            : null;

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

        if (engineEvent is not LockEvent and not WaitEvent
            && engineEvent.PlanNodeIdentifier is { } id
            && _ioOperatorNodes.TryGetValue(id, out var colour))
        {
            return colour;
        }

        return GetEventColour(engineEvent);
    }

    public Color? GetLatchMapColour(string? objectName) =>
        GetObjectColour(objectName) is { } colour ? Darken(colour, LatchDarkenFactor) : null;

    private static Color Darken(Color c, double factor) =>
        Color.FromArgb(c.A, (int)(c.R * factor), (int)(c.G * factor), (int)(c.B * factor));

    private static readonly Color StatementColour = Color.FromArgb(255, 130, 130, 130);

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
            LatchEvent => ColourConstants.LatchColour,
            WaitEvent => ColourConstants.WaitColour,
            TransactionLogEvent => ColourConstants.LogColour,
            _ => Color.Gray
        };
    }
}
