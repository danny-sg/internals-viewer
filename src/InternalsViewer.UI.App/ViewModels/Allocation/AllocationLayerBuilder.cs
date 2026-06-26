using InternalsViewer.Internals.Engine.Database;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using AllocationUnit = InternalsViewer.Internals.Engine.Database.AllocationUnit;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.UI.App.ViewModels.Allocation;

internal static class AllocationLayerBuilder
{
    private const int ColourCount = 360;
    private const int UserSaturation = 150;
    private const int UserValue = 220;

    public static List<AllocationLayer> GenerateLayers(DatabaseSource database, 
                                                       bool separateIndexes, 
                                                       bool isGreyScale = false)
    {
        var layers = new List<AllocationLayer>();

        var colourIndex = 0;

        var allocationUnits = database.AllocationUnits;

        var userObjectCount = allocationUnits.Values.Where(u => !u.IsSystem).DistinctBy(t => t.TableName).Count();

        foreach (var allocationUnit in allocationUnits.Values
                                                      .OrderBy(o => o.TableName)
                                                      .ThenBy(o => o.IndexName)
                                                      .Where(o => !o.IsSystem))
        {
            var currentObjectName = GetCurrentObjectName(allocationUnit, separateIndexes);

            if (layers.LastOrDefault()?.Name != currentObjectName)
            {
                var layer = CreateNewLayer(allocationUnit, 
                                           currentObjectName, 
                                           userObjectCount, 
                                           isGreyScale,
                                           ref colourIndex);

                layers.Add(layer);
            }

            layers.Last().Allocations.AddRange(GetExtentAllocations(allocationUnit.IamChain));

            layers.Last()
                  .SinglePages
                  .AddRange(allocationUnit.IamChain
                                          .Pages
                                          .SelectMany(s => s.SinglePageSlots)
                                          .Where(s => s != PageAddress.Empty));
        }

        var systemLayer = new AllocationLayer
        {
            Name = "System Objects",
            ObjectName = "System Objects",
            Colour = Color.FromArgb(255, 190, 190, 205),
            IsSystemObject = true,
            IsVisible = true
        };

        foreach (var systemAllocationUnit in allocationUnits.Values.Where(a => a.IsSystem))
        {
            systemLayer.Allocations.AddRange(GetExtentAllocations(systemAllocationUnit.IamChain));

            systemLayer.SinglePages
                       .AddRange(systemAllocationUnit.IamChain
                       .Pages
                       .SelectMany(s => s.SinglePageSlots)
                       .Where(s => s != PageAddress.Empty));

            systemLayer.TotalPages += systemAllocationUnit.TotalPages;
        }

        layers.Add(systemLayer);

        layers.Add(GenerateAllocationLayer("GAM", database.Gam, Color.CornflowerBlue));
        layers.Add(GenerateAllocationLayer("SGAM", database.SGam, Color.Green));

        return layers;
    }

    private static AllocationLayer GenerateAllocationLayer(string name, 
                                                           Dictionary<int, AllocationChain> allocations, 
                                                           Color colour,
                                                           bool isInverted)
    {
        var layer = new AllocationLayer
        {
            Name = name,
            Colour = colour,
            IsVisible = true
        };

        foreach (var allocation in allocations.Values)
        {
            layer.Allocations.AddRange(GetExtentAllocations(allocation));

        }

        return layer;
    }

    public static AllocationLayer GenerateLayer(AllocationPage allocationPage)
    {
        var layer = new AllocationLayer();
        var map = allocationPage.AllocationMap;
        var fileId = allocationPage.PageAddress.FileId;

        for (var i = 0; i < map.Length; i++)
        {
            if (map[i])
            {
                layer.Allocations.Add(new ExtentAllocation(fileId, i));
            }
        }

        return layer;
    }

    private static List<ExtentAllocation> GetExtentAllocations(IamChain chain)
    {
        var result = new List<ExtentAllocation>();

        foreach (var page in chain.Pages)
        {
            var map = page.AllocationMap;
            var fileId = page.StartPage.FileId;
            var baseExtent = page.StartPage.PageId * 8;

            for (var i = 0; i < map.Length; i++)
            {
                if (map[i])
                {
                    result.Add(new ExtentAllocation(fileId, baseExtent + i));
                }
            }
        }

        return result;
    }


    private static List<ExtentAllocation> GetExtentAllocations(AllocationChain chain)
    {
        var result = new List<ExtentAllocation>();

        foreach (var page in chain.Pages)
        {
            var map = page.AllocationMap;
            var fileId = page.StartPage.FileId;
            var baseExtent = page.StartPage.PageId * 8;

            for (var i = 0; i < map.Length; i++)
            {
                if (map[i])
                {
                    result.Add(new ExtentAllocation(fileId, baseExtent + i));
                }
            }
        }

        return result;
    }


    private static string GetCurrentObjectName(AllocationUnit allocationUnit, bool separateIndexes)
    {
        return separateIndexes && !string.IsNullOrEmpty(allocationUnit.IndexName)
            ? $"{allocationUnit.SchemaName}.{allocationUnit.TableName}.{allocationUnit.IndexName}"
            : $"{allocationUnit.SchemaName}.{allocationUnit.TableName}";
    }

    private static AllocationLayer CreateNewLayer(AllocationUnit allocationUnit,
                                                  string currentObjectName,
                                                  int userObjectCount,
                                                  bool isGreyScale,
                                                  ref int colourIndex)
    {
        var layer = new AllocationLayer
        {
            Name = currentObjectName,
            ObjectName = $"{allocationUnit.SchemaName}.{allocationUnit.TableName}",
            FirstPage = allocationUnit.FirstPage,
            RootPage = allocationUnit.RootPage,
            FirstIamPage = allocationUnit.FirstIamPage,
            IndexName = allocationUnit.IndexName,
            UsedPages = allocationUnit.UsedPages,
            TotalPages = allocationUnit.TotalPages,
            IndexType = allocationUnit.IndexType,
            IsSystemObject = allocationUnit.IsSystem,
            Colour = GetLayerColour(allocationUnit, userObjectCount, isGreyScale, ref colourIndex),
            IsVisible = true
        };

        return layer;
    }

    private static Color GetLayerColour(AllocationUnit allocationUnit, 
                                        int userObjectCount, 
                                        bool isGreyScale, 
                                        ref int colourIndex)
    {
        if (allocationUnit.IsSystem)
        {
            return Color.FromArgb(255, 190, 190, 205);
        }

        colourIndex += userObjectCount > ColourCount ? 1 : (int)Math.Floor(ColourCount / (double)userObjectCount);

        return isGreyScale
            ? GetGreyscaleColour(colourIndex)
            : ColourHelpers.HsvToColor(colourIndex, UserSaturation, UserValue);
    }

    private static Color GetGreyscaleColour(int colourIndex)
    {
        const int minBrightness = 60;
        const int maxBrightness = 140;

        var grey = minBrightness + (int)((double)colourIndex / ColourCount * (maxBrightness - minBrightness));

        return Color.FromArgb(255, Math.Clamp(grey, 0, 255), Math.Clamp(grey, 0, 255), Math.Clamp(grey, 0, 255));
    }
}
