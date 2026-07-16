using System;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Locks;

namespace InternalsViewer.UI.App.Controls.Timeline;

/// <summary>
/// The operator the user has selected, if any, and the object it accesses
/// </summary>
/// <remarks>
/// Held by the control and mutated in place on click/clear, so renderers can keep a single reference handed to them at
/// construction and always read the current selection without the control pushing updates.
/// </remarks>
internal sealed class CurrentSelection
{
    public int? NodeId { get; private set; }

    public string Schema { get; private set; } = string.Empty;

    public string Table { get; private set; } = string.Empty;

    public void Select(int nodeId, string schema, string table)
    {
        NodeId = nodeId;
        Schema = schema;
        Table = table;
    }

    public void Clear()
    {
        NodeId = null;
        Schema = string.Empty;
        Table = string.Empty;
    }

    // An event fades when an operator is selected and the event doesn't belong to it. Plan-matched events (reads, log)
    // compare on node id; locks carry no plan node, so they highlight with the selection when they are on the same
    // object (table) as the selected operator, and fade otherwise.
    public bool ShouldDim(EngineEvent ev)
    {
        if (NodeId is not { } selected)
        {
            return false;
        }

        if (ev.PlanNodeIdentifier is { } id)
        {
            return id.NodeId != selected;
        }

        if (ev is LockEvent && !string.IsNullOrEmpty(Table))
        {
            return !string.Equals(ev.TableName, Table, StringComparison.OrdinalIgnoreCase)
                   || !string.Equals(ev.SchemaName, Schema, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
