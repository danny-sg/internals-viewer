﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Engine;

#pragma warning disable CA1416

namespace InternalsViewer.UI.Allocations;

/// <summary>
/// Contains an Allocation structure to be displayed on the Allocation Map
/// </summary>
[Serializable]
public class AllocationLayer
{
    private Color colour;
    private int transparency = 40;

    public AllocationLayer()
    {
    }

    public AllocationLayer(IAllocationChain allocation)
    {
        Allocations.Add(allocation);
        Name = allocation.ToString() ?? string.Empty;
    }

    //public AllocationLayer(string name, AllocationChain allocationChain, Color colour)
    //{
    //    Name = name;
    //    Allocations.Add(allocationChain);
    //    this.colour = colour;
    //}

    public AllocationLayer(string name, AllocationPage page, Color colour)
    {
        Name = name;
        this.colour = colour;

        var chain = new AllocationChain();

        chain.Pages.Add(page);

        Allocations.Add(chain);
    }

    public static List<string> FindPage(PageAddress page, List<AllocationLayer> layers)
    {
        var layerNames = new List<string>();

        foreach (var layer in layers)
        {
            if (layer.FindPage(page, layer.IsInverted) != null)
            {
                layerNames.Add(layer.Name);
            }
        }

        return layerNames;
    }

    public static void RefreshLayers(List<AllocationLayer> layers)
    {
        foreach (var layer in layers)
        {
            foreach (var page in layer.Allocations)
            {
                // page.Refresh();
            }
        }
    }

    public AllocationLayer? FindExtent(int extent, short fileId, bool findInverted)
    {
        foreach (var chain in Allocations)
        {
            if (chain.IsExtentAllocated(extent, fileId, findInverted))
            {
                return this;
            }
        }

        return null;
    }

    public AllocationLayer? FindPage(PageAddress pageAddress, bool findInverted)
    {
        var extentAddress = pageAddress.PageId / 8;

        foreach (var chain in Allocations)
        {
            // Check if it's the actual IAM
            if (chain.Pages.Any(p => p.PageAddress == pageAddress))
            {
                return this;
            }

            if (chain.IsExtentAllocated(extentAddress, pageAddress.FileId, findInverted))
            {
                return this;
            }

            if (chain.SinglePageSlots.Contains(pageAddress))
            {
                return this;
            }
        }

        return null;
    }

    public bool IsTransparent { get; set; }

    public AllocationLayerType LayerType { get; set; }

    public Color Colour
    {
        get
        {
            if (IsTransparent)
            {
                return Color.FromArgb(transparency, colour);
            }

            return colour;
        }

        set => colour = value;
    }

    public string Name { get; set; } = string.Empty;

    public List<IAllocationChain> Allocations { get; set; } = new();

    public int Order { get; set; }

    public bool IsSystem { get; set; }

    public bool IsInverted { get; set; }

    public bool UseDefaultSinglePageColour { get; set; }

    public bool IsVisible { get; set; } = true;

    public Color BorderColour { get; set; }

    public bool UseBorderColour { get; set; }

    public bool SingleSlotsOnly { get; set; }

    public int Transparency { get; set; }

    public string IndexName { get; set; } = string.Empty;

    public IndexType IndexType { get; set; }

    public long UsedPages { get; set; }

    public long TotalPages { get; set; }

    public PageAddress FirstPage { get; set; }

    public PageAddress RootPage { get; set; }

    public PageAddress FirstIamPage { get; set; }

    public string ObjectName { get; set; } = string.Empty;
}