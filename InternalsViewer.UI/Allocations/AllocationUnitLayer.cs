using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.UI.Allocations
{
    public class AllocationUnitsLayer
    {
        private static readonly int colourCount = 360;
        private static readonly int userSaturation = 150;
        private static readonly int userValue = 220;

        /// <summary>
        /// Generates map layers by loading object's allocation structures
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="worker">The backgroundWorker object.</param>
        /// <returns></returns>
        public static List<AllocationLayer> GenerateLayers(Database database, BackgroundWorker worker, bool separateIndexes, bool separateSystemObjects)
        {
            List<AllocationLayer> layers = new List<AllocationLayer>();
            AllocationLayer layer = null;
            int colourIndex = 0;
            int count = 0;
            int systemColourIndex = 0;
            string previousObjectName = string.Empty;

            DataTable allocationUnits = database.AllocationUnits();

            string filter;

            if (separateIndexes)
            {
                filter = "allocation_unit_type=1 AND index_id < 2";
            }
            else
            {
                filter = string.Empty;
            }
            int userObjectCount = (int)allocationUnits.Compute("COUNT(table_name)", filter + " AND system=0");// "allocation_unit_type=1 AND system=0 AND index_id < 2");

            int systemObjectCount = (int)allocationUnits.Compute("COUNT(table_name)", filter + " AND system=1"); //, "allocation_unit_type=1 AND system=1 AND index_id < 2");

            foreach (DataRow row in allocationUnits.Rows)
            {
                if (worker.CancellationPending)
                {
                    return null;
                }

                count++;

                string currentObjectName;

                if ((bool)row["system"] && !separateSystemObjects)
                {
                    currentObjectName = "(System object)";
                }
                else
                {
                    if (separateIndexes)
                    {
                        currentObjectName = row["schema_name"] + "." + row["table_name"] + "." + row["index_name"];
                    }
                    else
                    {
                        currentObjectName = row["schema_name"] + "." + row["table_name"];
                    }
                }

                if (currentObjectName != previousObjectName)
                {
                    layer = new AllocationLayer();

                    layer.Name = row["schema_name"] + "." + row["table_name"];
                    layer.IndexName = row["index_name"].ToString();
                    layer.UsedPages = Convert.ToInt32(row["used_pages"]);
                    layer.TotalPages = Convert.ToInt32(row["total_pages"]);
                    layer.IndexType = (IndexTypes)Convert.ToInt32(row["index_type"]);

                    layer.UseDefaultSinglePageColour = false;

                    if ((bool)row["system"])
                    {
                        if (layer.Name != previousObjectName)
                        {
                            systemColourIndex += (int)Math.Floor(colourCount / (double)systemObjectCount);

                            if (colourIndex >= colourCount)
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
                            if (userObjectCount > colourCount)
                            {
                                colourIndex += 1;
                            }
                            else
                            {
                                colourIndex += (int)Math.Floor(colourCount / (double)userObjectCount);
                            }
                        }

                        layer.Colour = HsvColour.HsvToColor(colourIndex, userSaturation, userValue);
                    }

                    layers.Add(layer);
                }

                PageAddress address = new PageAddress((byte[])row["first_iam_page"]);

                if (address.PageId > 0)
                {
                    if (layer != null)
                    {
                        layer.Allocations.Add(new IamAllocation(database, address));
                    }
                }

                if (layer != null)
                {
                    previousObjectName = layer.Name;
                }

                worker.ReportProgress((int)(count / (float)allocationUnits.Rows.Count * 100), layer.Name);
            }

            return layers;
        }
    }
}
