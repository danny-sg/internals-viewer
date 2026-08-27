using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Models.Query.Trace;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed partial class TraceOperatorViewModel(int nodeId, string title, string description) : ObservableObject
{
    /// <summary>
    /// The iterator has been opened and not yet closed, which is while it has a position to show
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPositionVisible))]
    private bool _isOpen;

    /// <summary>
    /// The page the access path is reading, which with <see cref="CurrentSlot"/> is where it stands
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPositionVisible))]
    [NotifyPropertyChangedFor(nameof(CurrentRowIdentifier))]
    private PageAddress? _currentPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentRowIdentifier))]
    private int? _currentSlot;

    /// <summary>
    /// Whether the page visuals follow the page the operator is reading rather than showing the whole structure
    /// </summary>
    /// <remarks>
    /// One setting for the whole query rather than one per operator, so the value here is a copy the owner keeps in step with every other
    /// operator's. A panel writing to it is asking for the change, not making it.
    /// </remarks>
    [ObservableProperty]
    private bool _isZoomToPage;

    public event Action<int>? ActivationRequested;

    public event Action<PageAddress>? PageOpenRequested;

    public event Action<bool>? ZoomToPageRequested;
    public int NodeId { get; } = nodeId;

    public string Title { get; } = title;

    /// <summary>
    /// What the operator matches on, which is the one thing about it that is not visible in its inputs
    /// </summary>
    public string Description { get; } = description;

    public Uri? Icon { get; set; }

    public bool IsBatchMode { get; set; }

    public string ModeName => IsBatchMode ? "Batch Mode" : "Row Mode";

    public SolidColorBrush ModeBrush => new(IsBatchMode ? Colors.CadetBlue : Colors.Maroon);

    public bool IsPositionVisible => IsOpen && CurrentPage is not null;

    public string CurrentRowIdentifier => CurrentPage is { } page
        ? CurrentSlot is { } slot ? new RowIdentifier(page, (ushort)slot).ToString() : page.ToString()
        : string.Empty;

    public string Heading { get; set; } = "";

    public string Subheading { get; set; } = "";

    public JoinDecision? JoinRule { get; set; }

    public bool HasOutputPane { get; set; } = true;

    public bool IsOutputDefaultVisible { get; set; }

    public bool IsJoinLayout { get; set; }

    public TraceBlobPalette? BlobPalette { get; set; }

    public ObservableCollection<TraceInputRow> InputRows { get; } = [];

    public ObservableCollection<TraceStateItem> StateItems { get; } = [];

    public TracePane MainPane { get; set; } = TracePane.Empty;

    public TracePane OuterTop { get; set; } = TracePane.Empty;

    public TracePane OuterBottom { get; set; } = TracePane.Empty;

    public TracePane InnerTop { get; set; } = TracePane.Empty;

    public TracePane InnerBottom { get; set; } = TracePane.Empty;

    /// <summary>
    /// The page visuals among the operator's panes, which are the ones a zoom acts on
    /// </summary>
    /// <remarks>
    /// A columnstore visual has no pages to zoom to, so it is left out and an operator with only one offers no toggle.
    /// </remarks>
    public IEnumerable<TraceVisualViewModel> PageVisuals
        => new[] { MainPane, OuterTop, OuterBottom, InnerTop, InnerBottom }
            .Select(p => p.Content)
            .OfType<TraceVisualViewModel>()
            .Where(v => v.VisualType is TraceVisualType.Index or TraceVisualType.Allocation);

    public bool HasZoomToPage => PageVisuals.Any();

    public TraceRowStreamViewModel Output { get; } = new();

    public void RequestActivation(int targetNodeId) => ActivationRequested?.Invoke(targetNodeId);

    partial void OnIsZoomToPageChanged(bool value) => ZoomToPageRequested?.Invoke(value);

    public void RequestPageOpen()
    {
        if (CurrentPage is { } pageAddress)
        {
            PageOpenRequested?.Invoke(pageAddress);
        }
    }

    public void SetState(string name, string value)
    {
        foreach (var item in StateItems)
        {
            if (item.Name == name)
            {
                item.Value = value;

                return;
            }
        }
    }
}
