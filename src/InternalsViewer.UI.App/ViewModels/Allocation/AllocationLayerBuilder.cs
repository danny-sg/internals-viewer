using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using AllocationUnit = InternalsViewer.Internals.Engine.Database.AllocationUnit;

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
                                                      .ThenBy(o => 
                                                          o.AllocationUnitType == AllocationUnitType.InRowData ? 1 : 2)
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

            layers.Last().AllocationChains = [allocationUnit.IamChain];

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
            systemLayer.AllocationChains.Add(systemAllocationUnit.IamChain);

            systemLayer.SinglePages
                       .AddRange(systemAllocationUnit.IamChain
                                                     .Pages
                                                     .SelectMany(s => s.SinglePageSlots)
                                                     .Where(s => s != PageAddress.Empty));

            systemLayer.TotalPages += systemAllocationUnit.TotalPages;
        }

        layers.Add(systemLayer);

        layers.AddRange(GenerateAllocationLayers("GAM", database.Gam, Color.Green, true));
        layers.AddRange(GenerateAllocationLayers("SGAM", database.SGam, Color.OrangeRed, true));
        layers.AddRange(GenerateAllocationLayers("DCM", database.Dcm, Color.CornflowerBlue, true));
        layers.AddRange(GenerateAllocationLayers("BCM", database.Bcm, Color.Purple, true));

        var bufferPoolLayer = new AllocationLayer
        {
            Name = "Buffer Pool",
            LayerName = "Buffer Pool",
            Colour = Color.FromArgb(200, 190, 190, 205),
            IsVisible = true,
            Opacity = 0
        };

        layers.Add(bufferPoolLayer);

        return layers;
    }

    private static List<AllocationLayer> GenerateAllocationLayers(string name,
                                                                  Dictionary<int, AllocationChain> allocations,
                                                                  Color colour,
                                                                  bool isInverted)
    {
        var layer = new AllocationLayer
        {
            LayerName = name,
            Colour = colour,
            IsVisible = true,
            IsInverted = isInverted,
            AllocationChains = allocations.Values.Select(s => s).Cast<IAllocationChain>().ToList(),
            Opacity = 0
        };

        return [layer];
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
            IsVisible = true,
            Opacity = 100
        };

        return layer;
    }

    public static AllocationLayer GenerateLayer(AllocationPage allocationPage, int startOffset)
    {
        var layer = new AllocationLayer();

        var map = new BitmapAllocation(allocationPage.PageAddress.FileId, startOffset, allocationPage.AllocationMap);

        layer.AllocationChains = [map];

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
        const int minBrightness = 140;
        const int maxBrightness = 170;

        var grey = minBrightness + (int)((double)colourIndex / ColourCount * (maxBrightness - minBrightness));

        return Color.FromArgb(255, Math.Clamp(grey, 0, 255), Math.Clamp(grey, 0, 255), Math.Clamp(grey, 0, 255));
    }
}
