using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using AllocationUnit = InternalsViewer.Internals.Engine.Database.AllocationUnit;
using DatabaseFile = InternalsViewer.Internals.Engine.Database.DatabaseFile;

namespace InternalsViewer.UI.App.ViewModels.Allocation;

internal static class AllocationLayerBuilder
{
    private const int UserSaturation = 150;
    private const int UserValue = 220;

    // HsvToColor treats 256 hue steps as one revolution; we place objects across the wheel, so this is the wheel size.
    private const int HueWheel = 256;

    public static List<AllocationLayer> GenerateLayers(DatabaseSource database,
                                                       bool separateIndexes,
                                                       bool isDisplaySystemObjects,
                                                       byte opacity = 100)
    {
        var layers = new List<AllocationLayer>();

        var colourIndex = 0;

        var allocationUnits = database.AllocationUnits;

        var colourSlotCount = allocationUnits.Values
                                             .Where(u => !u.IsSystem || isDisplaySystemObjects)
                                             .Select(u => GetCurrentObjectName(u, separateIndexes))
                                             .Distinct()
                                             .Count();

        foreach (var allocationUnit in allocationUnits.Values
                                                      .OrderBy(o => o.TableName)
                                                      .ThenBy(o => o.IndexName)
                                                      .ThenBy(o => ScoreAllocationUnit(o))
                                                      .Where(o => !o.IsSystem || isDisplaySystemObjects))
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

            var lastLayer = layers.Last();

            lastLayer.AllocationChains.Add(allocationUnit.IamChain);

            foreach (var page in allocationUnit.IamChain.Pages)
            {
                foreach (var slot in page.SinglePageSlots)
                {
                    if (slot != PageAddress.Empty)
                    {
                        lastLayer.SinglePages.Add(slot);
                    }
                }

                if (page.PageAddress != PageAddress.Empty)
                {
                    lastLayer.SinglePages.Add(page.PageAddress);
                }
            }
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

        if (!isDisplaySystemObjects)
        {
            foreach (var systemAllocationUnit in allocationUnits.Values.Where(a => a.IsSystem))
            {
                systemLayer.AllocationChains.Add(systemAllocationUnit.IamChain);

                foreach (var page in systemAllocationUnit.IamChain.Pages)
                {
                    foreach (var slot in page.SinglePageSlots)
                    {
                        if (slot != PageAddress.Empty)
                        {
                            systemLayer.SinglePages.Add(slot);
                        }
                    }

                    if (page.PageAddress != PageAddress.Empty)
                    {
                        systemLayer.SinglePages.Add(page.PageAddress);
                    }
                }

                systemLayer.TotalPages += systemAllocationUnit.TotalPages;
            }

            layers.Add(systemLayer);
        }

        layers.Add(CreateDatabaseLayer(database));

        layers.AddRange(GenerateAllocationLayers("GAM", database.Gam, Color.Green, true));
        layers.AddRange(GenerateAllocationLayers("SGAM", database.SGam, Color.OrangeRed, false));
        layers.AddRange(GenerateAllocationLayers("DCM", database.Dcm, Color.CornflowerBlue, true));
        layers.AddRange(GenerateAllocationLayers("BCM", database.Bcm, Color.Purple, true));

        var bufferPoolLayer = new AllocationLayer
        {
            Name = "Buffer Pool",
            LayerName = "Buffer Pool",
            Colour = Color.FromArgb(255, 100, 100, 100),
            IsAllocationLayer = true,
            IsVisible = true,
            Opacity = 0,
            LayerType = LayerType.TopLeft
        };

        layers.Add(bufferPoolLayer);

        return layers;
    }

    private static int ScoreAllocationUnit(AllocationUnit allocationUnit)
    {
        if (allocationUnit.IndexType is IndexType.ClusteredColumnStore or IndexType.NonClusteredColumnStore)
        {
            return allocationUnit.AllocationUnitType == AllocationUnitType.LargeObjectData ? 1 : 2;
        }

        return allocationUnit.AllocationUnitType == AllocationUnitType.InRowData ? 1 : 2;
    }

    private static AllocationLayer CreateDatabaseLayer(DatabaseSource database)
    {
        var databaseLayer = new AllocationLayer
        {
            Name = "Database Pages",
            ObjectName = "Database Pages",
            Colour = Color.FromArgb(120, 100, 100, 205),
            IsSystemObject = true,
            IsAllocationLayer = true,
            IsVisible = true
        };

        foreach (var databaseFile in database.Files)
        {
            if (databaseFile.FileId == 1)
            {
                databaseLayer.SinglePages.Add(BootPage.BootPageAddress);
            }

            // File header
            databaseLayer.SinglePages.Add(new PageAddress(databaseFile.FileId, 0));

            if (databaseFile.FileType == FileType.Rows)
            {
                AddAllocationPages(databaseLayer, databaseFile);
            }
        }

        return databaseLayer;
    }

    private static void AddAllocationPages(AllocationLayer layer, DatabaseFile databaseFile)
    {
        var pageCount = databaseFile.Size;

        if (pageCount <= 0)
        {
            return;
        }

        var fileId = databaseFile.FileId;

        var extentCount = pageCount / 8;

        var allocationPageCount = Math.Max(1, (int)Math.Ceiling(extentCount / (decimal)AllocationPage.AllocationExtentInterval));

        int[] firstAllocationPages =
        [
            AllocationPage.FirstGamPage,
            AllocationPage.FirstSgamPage,
            AllocationPage.FirstDcmPage,
            AllocationPage.FirstBcmPage
        ];

        foreach (var firstPage in firstAllocationPages)
        {
            for (var i = 0; i < allocationPageCount; i++)
            {
                layer.SinglePages.Add(new PageAddress(fileId, firstPage + (i * AllocationPage.AllocationPageCount)));
            }
        }

        var pfsPageCount = Math.Max(1, (int)Math.Ceiling(pageCount / (decimal)PfsPage.PfsInterval));

        layer.SinglePages.Add(new PageAddress(fileId, PfsPage.FirstPfsPage));

        // The first PFS page is page 1, subsequent ones are the first page of their interval
        for (var i = 1; i < pfsPageCount; i++)
        {
            layer.SinglePages.Add(new PageAddress(fileId, i * PfsPage.PfsInterval));
        }
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
            AllocationChains = [.. allocations.Values.Select(s => s)],
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

    public static Color GetObjectColour(DatabaseSource database, AllocationUnit allocationUnit, bool separateIndexes = true)
    {
        if (allocationUnit.IsSystem)
        {
            return Color.FromArgb(255, 190, 190, 205);
        }

        var targetName = GetCurrentObjectName(allocationUnit, separateIndexes);

        var names = database.AllocationUnits
                            .Values
                            .OrderBy(o => o.TableName)
                            .ThenBy(o => o.IndexName)
                            .ThenBy(o => o.AllocationUnitType == AllocationUnitType.InRowData ? 1 : 2)
                            .Where(o => !o.IsSystem)
                            .Select(o => GetCurrentObjectName(o, separateIndexes))
                            .ToList();

        var colourSlotCount = names.Distinct().Count();

        var colourIndex = 0;

        string? previous = null;

        foreach (var name in names)
        {
            if (name == previous)
            {
                continue;
            }

            if (name == targetName)
            {
                break;
            }

            colourIndex++;

            previous = name;
        }

        var hue = colourIndex * HueWheel / Math.Max(colourSlotCount, 1) % HueWheel;

        return ColourHelpers.HsvToColor(hue, UserSaturation, UserValue);
    }

    private static Color GetLayerColour(AllocationUnit allocationUnit, int colourSlotCount, ref int colourIndex)
    {
        var hue = colourIndex * HueWheel / Math.Max(colourSlotCount, 1) % HueWheel;

        colourIndex++;

        return ColourHelpers.HsvToColor(hue, UserSaturation, UserValue);
    }
}
