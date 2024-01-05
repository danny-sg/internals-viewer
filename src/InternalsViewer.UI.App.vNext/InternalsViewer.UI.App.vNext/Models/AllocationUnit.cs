using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Interfaces.Engine;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalsViewer.UI.App.vNext.Models;

public class AllocationUnit: ObservableObject
{
}

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
    private List<int> allocations = new();

    [ObservableProperty]
    private bool isVisible;
}