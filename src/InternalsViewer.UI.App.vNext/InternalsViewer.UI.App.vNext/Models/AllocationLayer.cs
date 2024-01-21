using System.Collections.Generic;
using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database.Enums;

namespace InternalsViewer.UI.App.vNext.Models;

public partial class AllocationLayer : ObservableObject
{
    [ObservableProperty]
    private Color colour;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string objectName = string.Empty;

    [ObservableProperty]
    private string indexName  = string.Empty;

    [ObservableProperty]
    private IndexType indexType;

    [ObservableProperty]
    private bool isSystemObject;

    [ObservableProperty]
    private PageAddress firstPage;

    [ObservableProperty]
    private PageAddress rootPage;

    [ObservableProperty]
    private PageAddress firstIamPage;

    [ObservableProperty]
    private long usedPages;

    [ObservableProperty]
    private long totalPages;

    [ObservableProperty]
    private List<ExtentAllocation> allocations = new();

    [ObservableProperty]
    private List<PageAddress> singlePages = new();

    [ObservableProperty]
    private bool isVisible;
}

public class ExtentAllocation(short fileId, int extentId)
{
    public int ExtentId { get; set; } = extentId;

    public short FileId { get; set; } = fileId;
}