using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events;

namespace InternalsViewer.UI.App.Controls.EventGrid;

public sealed partial class EventGridRow(EngineEvent engineEvent, int depth, bool hasChildren, bool isExpanded)
    : ObservableObject
{
    public EngineEvent Event { get; } = engineEvent;

    public int Depth { get; } = depth;

    public bool HasChildren { get; } = hasChildren;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpanderGlyph))]
    private bool _isExpanded = isExpanded;

    public Thickness Indent => new(Depth * 16, 0, 0, 0);

    public Visibility ExpanderVisibility => HasChildren ? Visibility.Visible : Visibility.Collapsed;

    // ChevronDown (0xE70D) when open, ChevronRight (0xE76C) when closed (Segoe Fluent Icons).
    public string ExpanderGlyph => ((char)(IsExpanded ? 0xE70D : 0xE76C)).ToString();

    public PageAddress? PageAddress 
        => Event is PageEngineEvent pageEngineEvent ? pageEngineEvent.PageAddress : null;

    public RowIdentifier? RowIdentifier 
        => Event is RowIdentifierEngineEvent rowIdentifierEngineEvent ? rowIdentifierEngineEvent.RowIdentifier : null;
}
