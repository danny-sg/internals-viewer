using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;

namespace InternalsViewer.UI.Allocations;

public class AllocationUnitsLayerBuilder
{
    private const int ColourCount = 360;
    private const int UserSaturation = 150;
    private const int UserValue = 220;

    public static List<AllocationLayer> GenerateLayers(DatabaseDetail database, bool separateIndexes)
    {
        var layers = new List<AllocationLayer>();

        var colourIndex = 0;

        var allocationUnits = database.AllocationUnits;

        var userObjectCount = allocationUnits.Where(u => !u.IsSystem).DistinctBy(t => t.TableName).Count();

        foreach (var allocationUnit in allocationUnits.OrderBy(o => o.TableName).ThenBy(o => o.IndexName))
        {
            var currentObjectName = GetCurrentObjectName(allocationUnit, separateIndexes);

            if (layers.LastOrDefault()?.Name != currentObjectName)
            {
                var layer = CreateNewLayer(allocationUnit, currentObjectName, userObjectCount, ref colourIndex);
                layers.Add(layer);
            }

            layers.Last().Allocations.Add(allocationUnit.IamChain);
        }

        return layers;
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
            IndexType = (IndexType)allocationUnit.IndexType,
            IsSystem = allocationUnit.IsSystem,
            UseDefaultSinglePageColour = false,
            Colour = GetLayerColour(allocationUnit, userObjectCount, ref colourIndex)
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

        return HsvColour.HsvToColor(colourIndex, UserSaturation, UserValue);
    }
}