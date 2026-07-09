using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.UI.App.Models;

public partial class AllocationLayer : ObservableObject
{
    [ObservableProperty]
    private Color _colour;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _objectName = string.Empty;

    [ObservableProperty]
    private string _indexName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IndexTypeDescription))]
    [NotifyPropertyChangedFor(nameof(IsIndex))]
    private IndexType _indexType;

    public bool IsIndex => IndexType is IndexType.Clustered or IndexType.NonClustered;

    public string IndexTypeDescription => IsSystemObject ? string.Empty : IndexType.ToString().SplitCamelCase("-");

    [ObservableProperty]
    private bool _isSystemObject;

    // True for the static per-table/system/allocation-map layers built by AllocationLayerBuilder, as
    // opposed to the dynamic event-overlay layers (I/O, Lock, Latch, ...) built per query run. These
    // always render at full opacity - see AllocationControl.DrawExtents - since they're the map's own
    // colouring, not something that should get dimmed just because an unrelated overlay is selected.
    [ObservableProperty]
    private bool _isAllocationLayer;

    [ObservableProperty]
    private PageAddress _firstPage;

    [ObservableProperty]
    private PageAddress _rootPage;

    [ObservableProperty]
    private PageAddress _firstIamPage;

    [ObservableProperty]
    private long _usedPages;

    [ObservableProperty]
    private long _totalPages;

    [ObservableProperty]
    private List<IAllocationChain> _allocationChains = [];

    [ObservableProperty]
    private List<PageAddress> _singlePages = [];

    [ObservableProperty]
    private List<PageSpan> _pageSpans = [];

    // Sorted ascending by StartUs so DrawExtents can binary-search to the current playhead instead of
    // scanning every span every frame. Set together with MaxFlashDurationUs - see AllocationLayer.SetFlashSpans.
    [ObservableProperty]
    private IReadOnlyList<PageFlashSpan> _flashSpans = [];

    // The longest (StartUs..EndUs) width in FlashSpans. Bounds how far back a playhead-time lookup ever
    // needs to scan: any span starting before (playhead - MaxFlashDurationUs) cannot still be active.
    [ObservableProperty]
    private long _maxFlashDurationUs;

    /// <summary>Sets <see cref="FlashSpans"/> (sorted by start time) and its matching duration bound together.</summary>
    public void SetFlashSpans(IReadOnlyList<PageFlashSpan> spans)
    {
        FlashSpans = spans;
        MaxFlashDurationUs = spans.Count == 0 ? 0 : spans.Max(s => s.EndUs - s.StartUs);
    }

    [ObservableProperty]
    private bool _isInverted;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty] 
    private byte _opacity = 100;

    public string LayerName { get; set; } = string.Empty;
}