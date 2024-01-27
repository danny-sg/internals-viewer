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
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.UI.App.ViewModels.Allocation;

internal static class AllocationLayerBuilder
{
    private const int ColourCount = 360;
    private const int UserSaturation = 150;
    private const int UserValue = 220;

    public static List<AllocationLayer> GenerateLayers(DatabaseSource database, bool separateIndexes)
    {
        var layers = new List<AllocationLayer>();

        var colourIndex = 0;

        var allocationUnits = database.AllocationUnits;

        var userObjectCount = allocationUnits.Where(u => !u.IsSystem).DistinctBy(t => t.TableName).Count();

        foreach (var allocationUnit in allocationUnits.OrderBy(o => o.TableName).ThenBy(o => o.IndexName).Where(o => !o.IsSystem))
        {
            var currentObjectName = GetCurrentObjectName(allocationUnit, separateIndexes);

            if (layers.LastOrDefault()?.Name != currentObjectName)
            {
                var layer = CreateNewLayer(allocationUnit, currentObjectName, userObjectCount, ref colourIndex);

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

        foreach (var systemAllocationUnit in allocationUnits.Where(a => a.IsSystem))
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

        return layers;
    }

    public static AllocationLayer GenerateLayer(AllocationPage allocationPage)
    {
        var layer = new AllocationLayer();

        layer.Allocations
             .AddRange(allocationPage.AllocationMap
                                     .Select((isAllocated, index) => new
                                     {
                                         isAllocated,
                                         Extent = index,
                                         allocationPage.PageAddress.FileId
                                     })
                                     .Where(w => w.isAllocated)
                                     .Select(s => new ExtentAllocation(s.FileId, s.Extent)));

        return layer;
    }

    private static List<ExtentAllocation> GetExtentAllocations(IamChain chain)
    {
        var result = new List<ExtentAllocation>();

        result.AddRange(chain.Pages
                             .SelectMany(s => s.AllocationMap
                                               .Select((isAllocated, index) => new
                                               {
                                                   isAllocated,
                                                   Extent = index + s.StartPage.PageId * 8,
                                                   s.StartPage.FileId
                                               }))
                             .Where(w => w.isAllocated)
                             .Select(s => new ExtentAllocation(s.FileId, s.Extent)));

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
            Colour = GetLayerColour(allocationUnit, userObjectCount, ref colourIndex),
            IsVisible = true
        };

        return layer;
    }

    private static Color GetLayerColour(AllocationUnit allocationUnit, int userObjectCount, ref int colourIndex)
    {
        if (allocationUnit.IsSystem)
        {
            return Color.FromArgb(255, 190, 190, 205);
        }

        colourIndex += userObjectCount > ColourCount ? 1 : (int)Math.Floor(ColourCount / (double)userObjectCount);

        return ColourHelpers.HsvToColor(colourIndex, UserSaturation, UserValue);
    }
}
