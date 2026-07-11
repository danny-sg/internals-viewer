using InternalsViewer.Query.Events.EventTypes;
using Microsoft.UI.Xaml;

namespace InternalsViewer.UI.App.Controls.EventGrid;

/// <summary>
/// One row in the events grid: an event plus the tree state needed to render it flattened with indentation
/// </summary>
/// <remarks>
/// The grid stays a flat DataGrid; hierarchy (a read group over its child events) is achieved by flattening the tree
/// into indented rows with an expander, so filtering, sorting, selection and row highlighting all keep working. Rows
/// are rebuilt on expand/collapse, so the expander state is captured at construction rather than observed.
/// </remarks>
public sealed class EventRow(EngineEvent engineEvent, int depth, bool hasChildren, bool isExpanded)
{
    public EngineEvent Event { get; } = engineEvent;

    public int Depth { get; } = depth;

    public bool HasChildren { get; } = hasChildren;

    public bool IsExpanded { get; } = isExpanded;

    public Thickness Indent => new(Depth * 16, 0, 0, 0);

    public Visibility ExpanderVisibility => HasChildren ? Visibility.Visible : Visibility.Collapsed;

    // ChevronDown (0xE70D) when open, ChevronRight (0xE76C) when closed (Segoe Fluent Icons).
    public string ExpanderGlyph => ((char)(IsExpanded ? 0xE70D : 0xE76C)).ToString();
}
