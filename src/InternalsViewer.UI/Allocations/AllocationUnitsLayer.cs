using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Records.Index;

#pragma warning disable CA1416

namespace InternalsViewer.UI.Allocations;

public class AllocationUnitsLayer
{
    private const int ColourCount = 360;
    private const int UserSaturation = 150;
    private const int UserValue = 220;

    /// <summary>
    /// Generates map layers by loading object's allocation structures
    /// </summary>
    public static List<AllocationLayer>? GenerateLayers(DatabaseDetail databaseDetail,
                                                        BackgroundWorker worker,
                                                        bool separateIndexes)
    {
        var layers = new List<AllocationLayer>();

        AllocationLayer? layer = null;

        var colourIndex = 0;
        
        var count = 0;
        var previousObjectName = string.Empty;

        var allocationUnits = databaseDetail.AllocationUnits;

        var userObjectCount = allocationUnits.Where(u => !u.IsSystem).DistinctBy(t => t.TableName).Count();

        foreach (var allocationUnit in allocationUnits.OrderBy(o => o.TableName).ThenBy(o => o.IndexName))
        {
            if (worker.CancellationPending)
            {
                return null;
            }

            count++;

            string currentObjectName;

            if (separateIndexes && !string.IsNullOrEmpty(allocationUnit.IndexName))
            {
                currentObjectName = allocationUnit.SchemaName + "." + allocationUnit.TableName + "." + allocationUnit.IndexName;
            }
            else
            {
                currentObjectName = allocationUnit.SchemaName + "." + allocationUnit.TableName;
            }

            if (currentObjectName != previousObjectName)
            {
                previousObjectName = currentObjectName;

                layer = new AllocationLayer();

                layer.Name = currentObjectName;
                layer.ObjectName = allocationUnit.SchemaName + "." + allocationUnit.TableName;
                layer.FirstPage = allocationUnit.FirstPage;
                layer.RootPage = allocationUnit.RootPage;
                layer.FirstIamPage = allocationUnit.FirstIamPage;

                layer.IndexName = allocationUnit.IndexName;
                layer.UsedPages = allocationUnit.UsedPages;
                layer.TotalPages = allocationUnit.TotalPages;
                layer.IndexType = (IndexTypes)allocationUnit.IndexType;

                layer.IsSystem = allocationUnit.IsSystem;

                layer.UseDefaultSinglePageColour = false;

                if(allocationUnit.IsSystem)
                {
                    layer.Colour = Color.FromArgb(255, 190, 190, 205);
                }
                else
                {
                    if (userObjectCount > ColourCount)
                    {
                        colourIndex += 1;
                    }
                    else
                    {
                        colourIndex += (int)Math.Floor(ColourCount / (double)userObjectCount);
                    }

                    layer.Colour = HsvColour.HsvToColor(colourIndex, UserSaturation, UserValue);
                }

                var address = allocationUnit.FirstIamPage;

                if (address.PageId > 0)
                {
                    layer.Allocations.Add(allocationUnit.IamChain);
                }

                layers.Add(layer);
            }

     

            worker.ReportProgress((int)(count / (float)allocationUnits.Count * 100), layer.Name);
        }

 

        return layers;
    }
}