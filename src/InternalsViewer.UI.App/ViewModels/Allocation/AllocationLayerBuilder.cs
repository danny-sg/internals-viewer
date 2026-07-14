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
    private const int UserSaturation = 150;
    private const int UserValue = 220;

    // HsvToColor treats 256 hue steps as one revolution; we place objects across the wheel, so this is the wheel size.
    private const int HueWheel = 256;

    public static List<AllocationLayer> GenerateLayers(DatabaseSource database,
                                                       bool separateIndexes,
                                                       byte opacity = 100)
    {
        var layers = new List<AllocationLayer>();

        var colourIndex = 0;

        var allocationUnits = database.AllocationUnits;

        // One colour slot per layer, keyed by the SAME name the layers are grouped on below — so the spacing divisor
        // matches the number of colours actually assigned (DisplayName can differ from the layer key under
        // separateIndexes, which is what left neighbouring objects sharing near-identical hues).
        var colourSlotCount = allocationUnits.Values
                                             .Where(u => !u.IsSystem)
                                             .Select(u => GetCurrentObjectName(u, separateIndexes))
                                             .Distinct()
                                             .Count();

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
                                           colourSlotCount,
                                           opacity,
                                           ref colourIndex);

                layers.Add(layer);
            }

            layers.Last().AllocationChains.Add(allocationUnit.IamChain);

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
            IsAllocationLayer = true,
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
            Colour = Color.FromArgb(200, 190, 100, 100),
            IsAllocationLayer = true,
            IsVisible = true,
            Opacity = 0,
            LayerType = LayerType.TopLeft
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
            IsAllocationLayer = true,
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
                                                  int colourSlotCount,
                                                  byte opacity,
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
            IsAllocationLayer = false,
            Colour = GetLayerColour(allocationUnit, colourSlotCount, ref colourIndex),
            IsVisible = true,
            Opacity = opacity
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
                                        int colourSlotCount,
                                        ref int colourIndex)
    {
        if (allocationUnit.IsSystem)
        {
            return Color.FromArgb(255, 190, 190, 205);
        }

        // Object i sits at i/N of the wheel (i = 0..N-1), so every neighbour — the last→first wrap included — is the
        // same 360/N apart: the arrangement that maximises the MINIMUM distance between any two objects. It never
        // reaches HueWheel, so no object lands on the 0°/360° seam and collides with the first.
        var hue = colourIndex * HueWheel / Math.Max(colourSlotCount, 1) % HueWheel;

        colourIndex++;

        return ColourHelpers.HsvToColor(hue, UserSaturation, UserValue);
    }
}
