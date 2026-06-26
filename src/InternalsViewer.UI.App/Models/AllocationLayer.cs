using System.Collections.Generic;
using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Helpers;

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
    private List<ExtentAllocation> _allocations = [];

    [ObservableProperty]
    private List<PageAddress> _singlePages = [];

    [ObservableProperty]
    private List<PageSpan> _pageSpans = [];

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty] 
    private byte _opacity = 100;
}