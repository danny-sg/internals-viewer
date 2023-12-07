using System.Collections.Generic;
using System.Drawing;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Pages;
using System;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.UI.Allocations;

/// <summary>
/// Contains an Allocation structure to be displayed on the Allocation Map
/// </summary>
[Serializable]
public class AllocationLayer
{
    private List<Allocation> allocations = new();
    private Color borderColour;
    private Color colour;
    private bool invert;
    private string name = string.Empty;
    private int order;
    private bool singleSlotsOnly = false;
    private bool transparent;
    private bool useBorderColour;
    private AllocationLayerType layerType = AllocationLayerType.Standard;
    private int transparency = 40;
    private bool useDefaultSinglePageColour = false;
    private bool visible = true;
    private string indexName = string.Empty;
    private int usedPages;
    private IndexTypes indexType;
    private int totalPages;
    private string objectName;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllocationLayer"/> class.
    /// </summary>
    public AllocationLayer()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AllocationLayer"/> class.
    /// </summary>
    /// <param name="page">The allocation.</param>
    public AllocationLayer(Allocation page)
    {
        allocations.Add(page);
        name = page.ToString();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AllocationLayer"/> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="allocation">The allocation.</param>
    /// <param name="colour">The colour of the layer.</param>
    public AllocationLayer(string name, Allocation allocation, Color colour)
    {
        this.name = name;
        allocations.Add(allocation);
        this.colour = colour;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AllocationLayer"/> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="page">The page.</param>
    /// <param name="colour">The colour of the layer.</param>
    public AllocationLayer(string name, AllocationPage page, Color colour)
    {
        this.name = name;

        if (page.Header.PageType == PageType.Iam)
        {
            allocations.Add(new IamAllocation(page));
        }
        else
        {
            allocations.Add(new Allocation(page));
        }

        this.colour = colour;
    }

    /// <summary>
    /// Finds a page.
    /// </summary>
    /// <param name="page">The page address.</param>
    /// <param name="layers">The layers to search.</param>
    /// <returns></returns>
    public static List<string> FindPage(PageAddress page, List<AllocationLayer> layers)
    {
        var layerNames = new List<string>();

        foreach (var layer in layers)
        {
            if (layer.FindPage(page, layer.Invert) != null)
            {
                layerNames.Add(layer.Name);
            }
        }

        return layerNames;
    }

    /// <summary>
    /// Refreshes the layers.
    /// </summary>
    /// <param name="layers">The layers.</param>
    public static void RefreshLayers(List<AllocationLayer> layers)
    {
        foreach (var layer in layers)
        {
            foreach (var page in layer.Allocations)
            {
                page.Refresh();
            }
        }
    }

    /// <summary>
    /// Finds an allocated extent in the layer.
    /// </summary>
    /// <param name="extent">The extent.</param>
    /// <param name="fileId">The file id.</param>
    /// <param name="findInverted">if set to <c>true</c> [find inverted].</param>
    /// <returns></returns>
    public AllocationLayer FindExtent(int extent, int fileId, bool findInverted)
    {
        foreach (var alloc in allocations)
        {
            if (Allocation.CheckAllocationStatus(extent, fileId, findInverted, alloc))
            {
                return this;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds a page.
    /// </summary>
    /// <param name="pageAddress">The page address.</param>
    /// <param name="findInverted">if set to <c>true</c> [find inverted].</param>
    /// <returns></returns>
    public AllocationLayer FindPage(PageAddress pageAddress, bool findInverted)
    {
        var extentAddress = pageAddress.PageId / 8;

        foreach (var alloc in allocations)
        {
            // Check if it's the actual IAM
            if (alloc.Pages.Exists(p => p.PageAddress == pageAddress))
            {
                return this;
            }

            if (Allocation.CheckAllocationStatus(extentAddress, pageAddress.FileId, findInverted, alloc))
            {
                return this;
            }

            if (alloc.SinglePageSlots.Contains(pageAddress))
            {
                return this;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="AllocationLayer"/> is transparent.
    /// </summary>
    /// <value><c>true</c> if transparent; otherwise, <c>false</c>.</value>
    public bool Transparent
    {
        get => transparent;
        set => transparent = value;
    }

    /// <summary>
    /// Gets or sets the type of the layer.
    /// </summary>
    /// <value>The type of the layer.</value>
    public AllocationLayerType LayerType
    {
        get => layerType;
        set => layerType = value;
    }

    /// <summary>
    /// Gets or sets the layer colour.
    /// </summary>
    /// <value>The layer colour.</value>
    public Color Colour
    {
        get
        {
            if (transparent)
            {
                return Color.FromArgb(transparency, colour);
            }

            return colour;
        }

        set => colour = value;
    }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public string Name
    {
        get => name;
        set => name = value;
    }

    /// <summary>
    /// Gets the allocations.
    /// </summary>
    /// <value>The allocations.</value>
    public List<Allocation> Allocations
    {
        get => allocations;
        set => allocations = value;
    }

    /// <summary>
    /// Gets or sets the order.
    /// </summary>
    /// <value>The order.</value>
    public int Order
    {
        get => order;
        set => order = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="AllocationLayer"/> is invert.
    /// </summary>
    /// <value><c>true</c> if invert; otherwise, <c>false</c>.</value>
    public bool Invert
    {
        get => invert;
        set => invert = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether to use the default single page colour.
    /// </summary>
    /// <value>
    /// 	<c>true</c> if use default single page colour; otherwise, <c>false</c>.
    /// </value>
    public bool UseDefaultSinglePageColour
    {
        get => useDefaultSinglePageColour;
        set => useDefaultSinglePageColour = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="AllocationLayer"/> is visible.
    /// </summary>
    /// <value><c>true</c> if visible; otherwise, <c>false</c>.</value>
    public bool Visible
    {
        get => visible;
        set => visible = value;
    }

    /// <summary>
    /// Gets or sets the border colour.
    /// </summary>
    /// <value>The border colour.</value>
    public Color BorderColour
    {
        get => borderColour;
        set => borderColour = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether to use border colour.
    /// </summary>
    /// <value><c>true</c> if [use border colour]; otherwise, <c>false</c>.</value>
    public bool UseBorderColour
    {
        get => useBorderColour;
        set => useBorderColour = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether to display single slots only.
    /// </summary>
    /// <value><c>true</c> if [single slots only]; otherwise, <c>false</c>.</value>
    public bool SingleSlotsOnly
    {
        get => singleSlotsOnly;
        set => singleSlotsOnly = value;
    }

    /// <summary>
    /// Gets or sets the transparency level.
    /// </summary>
    /// <value>The transparency level.</value>
    public int Transparency
    {
        get => transparency;
        set => transparency = value;
    }

    public string IndexName
    {
        get => indexName;
        set => indexName = value;
    }

    public IndexTypes IndexType
    {
        get => indexType;
        set => indexType = value;
    }
    public int UsedPages
    {
        get => usedPages;
        set => usedPages = value;
    }

    public int TotalPages
    {
        get => totalPages;
        set => totalPages = value;
    }

    public string ObjectName
    {
        get => objectName;
        set => objectName = value;
    }
}