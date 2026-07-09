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
    // Darkening factor applied to an object's own colour for a latch tied to that object - same hue,
    // dark enough to read as "held, not read" against the object's own (brighter) I/O.
    private const double LatchDarkenFactor = 0.35;

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

    /// <summary>
    /// The original (non-greyscale) allocation-unit colour for <paramref name="objectName"/> (e.g.
    /// "dbo.Table" or "dbo.Table.Index") - the same colour that object's pages show on the allocation
    /// map. Used for an operator's corner marker. Null when the name doesn't resolve to a real object
    /// (blank, or a system/tracking page like PFS).
    /// </summary>
    public Color? GetObjectColour(string? objectName) =>
        !string.IsNullOrEmpty(objectName) && _objectColours.TryGetValue(objectName, out var colour)
            ? colour
            : null;

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
        // representing that operator's IO, so they keep their own colour rather than being tinted like
        // the data-access events. A latch, though, marks an actual buffer-pool page touch for that
        // operator - the same signal I/O carries - so a latch tied to an operator is tinted the same as
        // that operator's I/O, and only falls back to the flat latch colour when it isn't linked to one.
        if (engineEvent is not LockEvent and not WaitEvent
            && engineEvent.PlanNodeIdentifier is { } id
            && _ioOperatorNodes.TryGetValue(id, out var colour))
        {
            return colour;
        }

        return GetEventColour(engineEvent);
    }

    /// <summary>
    /// The allocation-map-only colour for a latch on <paramref name="objectName"/> - a dark version of
    /// that object's own (non-greyscale) colour, distinct from <see cref="GetColour"/> (which is what the
    /// timeline uses, and deliberately unaffected by this - the object colour is for the allocation map
    /// alone). Null when the name doesn't resolve to a real object (blank, or a system/tracking page).
    /// </summary>
    public Color? GetLatchMapColour(string? objectName) =>
        GetObjectColour(objectName) is { } colour ? Darken(colour, LatchDarkenFactor) : null;

    /// <summary>Scales a colour's RGB channels down towards black by <paramref name="factor"/>, preserving alpha.</summary>
    private static Color Darken(Color c, double factor) =>
        Color.FromArgb(c.A, (int)(c.R * factor), (int)(c.G * factor), (int)(c.B * factor));

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
            LatchEvent => ColourConstants.LatchColour,
            WaitEvent => ColourConstants.WaitColour,
            TransactionLogEvent => ColourConstants.LogColour,
            _ => Color.Gray
        };
    }
}
