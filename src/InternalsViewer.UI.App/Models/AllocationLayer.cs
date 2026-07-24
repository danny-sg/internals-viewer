using System.Collections.Generic;
using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.UI.App.Helpers;
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
    [NotifyPropertyChangedFor(nameof(IndexTypeDescription))]
    [NotifyPropertyChangedFor(nameof(IsIndex))]
    private IndexType _indexType;

    public bool IsIndex => IndexType is IndexType.Clustered or IndexType.NonClustered && TotalPages > 0;

    public string IndexTypeDescription => IsSystemObject ? string.Empty : IndexType.ToString().SplitCamelCase("-");

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

public enum LayerType
{
    Fill,
    TopLeft
}