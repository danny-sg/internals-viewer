using System.Collections.Generic;
using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Allocations;
using SkiaSharp;

namespace InternalsViewer.UI.App.Models;

public sealed partial class AllocationLayer : ObservableObject
{
    [ObservableProperty]
    private Color _colour;

    private SKColor? _rendererColour;

    public SKColor RendererColour
    {
        get
        {
            _rendererColour ??= Colour.ToSkColor();

            return _rendererColour.Value;
        }
    }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _objectName = string.Empty;

    [ObservableProperty]
    private string _indexName = string.Empty;

    [ObservableProperty]
    private bool _isPartitioned;

    [ObservableProperty]
    private List<AllocationLayerUnit> _units = [];

    /// <summary>
    /// Identifies the allocation unit a viewer opened from this layer should load
    /// </summary>
    [ObservableProperty]
    private long _allocationUnitId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IndexTypeDescription))]
    [NotifyPropertyChangedFor(nameof(IsIndex))]
    private IndexType _indexType;

    public bool IsIndex => IndexType is IndexType.Clustered or IndexType.NonClustered && TotalPages > 0;

    public bool IsColumnstore => IndexType is IndexType.ClusteredColumnStore or IndexType.NonClusteredColumnStore && TotalPages > 0;

    public string IndexTypeDescription => IndexType.ToString().SplitCamelCase();

    [ObservableProperty]
    private bool _isSystemObject;

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
    private IReadOnlyList<PageSpan> _pageSpans = [];

    public void SetPageSpans(IReadOnlyList<PageSpan> spans)
    {
        PageSpans = spans;
    }

    [ObservableProperty]
    private bool _isInverted;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private byte _opacity = 100;

    public string LayerName { get; set; } = string.Empty;

    public LayerType LayerType { get; set; } = LayerType.Fill;
}