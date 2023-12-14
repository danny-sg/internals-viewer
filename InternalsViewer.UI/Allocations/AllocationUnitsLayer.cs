using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Engine.Database;

#pragma warning disable CA1416

namespace InternalsViewer.UI.Allocations;

public class AllocationUnitsLayer
{
    private static readonly int ColourCount = 360;
    private static readonly int UserSaturation = 150;
    private static readonly int UserValue = 220;

    /// <summary>
    /// Generates map layers by loading object's allocation structures
    /// </summary>
    public static List<AllocationLayer> GenerateLayers(Database database, BackgroundWorker worker, bool separateIndexes, bool separateSystemObjects)
    {
        var layers = new List<AllocationLayer>();
        AllocationLayer layer = null;
        var colourIndex = 0;
        var count = 0;
        var systemColourIndex = 0;
        var previousObjectName = string.Empty;

        var allocationUnits = database.AllocationUnits;

        string filter;

        if (separateIndexes)
        {
            filter = "allocation_unit_type=1 AND index_id < 2";
        }
        else
        {
            filter = string.Empty;
        }

        var userObjectCount = allocationUnits.Where(u => !u.IsSystem).DistinctBy(t => t.TableName).Count();

        var systemObjectCount = allocationUnits.Where(u => u.IsSystem).DistinctBy(t => t.TableName).Count();

        foreach (var allocationUnit in allocationUnits)
        {
            if (worker.CancellationPending)
            {
                return null;
            }

            count++;

            string currentObjectName;

            if ((bool)allocationUnit.IsSystem && !separateSystemObjects)
            {
                currentObjectName = "(System object)";
            }
            else
            {
                if (separateIndexes && !string.IsNullOrEmpty(allocationUnit.IndexName))
                {

                    currentObjectName = allocationUnit.SchemaName + "." + allocationUnit.TableName + "." + allocationUnit.IndexName;
                }
                else
                {
                    currentObjectName = allocationUnit.SchemaName + "." + allocationUnit.TableName;
                }
            }

            if (currentObjectName != previousObjectName)
            {
                layer = new AllocationLayer();

                layer.Name = currentObjectName;
                layer.ObjectName = allocationUnit.SchemaName + "." + allocationUnit.TableName;

                if (!allocationUnit.IsSystem)
                {
                    layer.IndexName = allocationUnit.IndexName;
                    layer.UsedPages = allocationUnit.UsedPages;
                    layer.TotalPages = allocationUnit.TotalPages;
                    layer.IndexType = (IndexTypes)allocationUnit.IndexType;
                }

                layer.UseDefaultSinglePageColour = false;

                if (allocationUnit.IsSystem)
                {
                    if (layer.Name != previousObjectName)
                    {
                        systemColourIndex += (int)Math.Floor(ColourCount / (double)systemObjectCount);

                        if (colourIndex >= ColourCount)
                        {
                            colourIndex = 1;
                        }
                    }

                    layer.Colour = Color.FromArgb(255, 190, 190, 205);
                }
                else
                {
                    if (layer.Name != previousObjectName)
                    {
                        if (userObjectCount > ColourCount)
                        {
                            colourIndex += 1;
                        }
                        else
                        {
                            colourIndex += (int)Math.Floor(ColourCount / (double)userObjectCount);
                        }
                    }

                    layer.Colour = HsvColour.HsvToColor(colourIndex, UserSaturation, UserValue);
                }

                layers.Add(layer);
            }

            var address = allocationUnit.FirstIamPage;

            if (address.PageId > 0)
            {
                if (layer != null)
                {
                   // layer.Allocations.Add(new IamChain(database, address));
                }
            }

            if (layer != null)
            {
                previousObjectName = layer.Name;
            }

            worker.ReportProgress((int)(count / (float)allocationUnits.Count * 100), layer.Name);
        }

        return layers;
    }
}