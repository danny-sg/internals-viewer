using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Pages;
using InternalsViewer.UI.Allocations;

namespace InternalsViewer.UI
{
    internal class FullMapRenderer
    {
        /// <summary>
        /// Renders the map layers and returns a bitmap
        /// </summary>
        /// <param name="worker">The worker.</param>
        /// <param name="mapLayers">The map layers.</param>
        /// <param name="rect">The rect.</param>
        /// <param name="fileId">The file id.</param>
        /// <param name="fileSize">Size of the file.</param>
        /// <returns></returns>
        public static Bitmap RenderMapLayers(BackgroundWorker worker, List<AllocationLayer> mapLayers, Rectangle rect, int fileId, int fileSize)
        {
            var stopWatch = new Stopwatch();

            stopWatch.Start();

            fileSize = fileSize / 8;

            var fileRectange = GetFileRectange(rect, fileSize);

            var bitmap = new Bitmap(fileRectange.Width, fileRectange.Height, PixelFormat.Format24bppRgb);

            var returnBitmap = new Bitmap(rect.Width, rect.Height);

            using (var g = Graphics.FromImage(returnBitmap))
            {
                using (var brush = new LinearGradientBrush(rect, Color.White, Color.Gainsboro, 1.25F))
                {
                    // Draw the background gradient
                    g.FillRectangle(brush, rect);
                }

                foreach (var layer in mapLayers)
                {
                    foreach (var allocation in layer.Allocations)
                    {
                        foreach (var page in allocation.Pages)
                        {
                            if (page.StartPage.FileId == fileId)
                            {
                                // Add the allocation to the bitmap with the given layer colour
                                AddAllocationToBitmap(bitmap, page.AllocationMap, page.StartPage, fileSize, layer.Colour);
                            }
                        }
                    }
                }

                stopWatch.Stop();

                Debug.Print("Render time: {0}", stopWatch.Elapsed.TotalSeconds);

                bitmap.MakeTransparent(Color.Black);

                g.InterpolationMode = InterpolationMode.NearestNeighbor;

                g.DrawImage(bitmap, 0, 0, rect.Width, rect.Height);
            }

            bitmap.Dispose();

            return returnBitmap;
        }

        /// <summary>
        /// Gets the file rectange for the file bitmap
        /// </summary>
        /// <param name="rect">The rect.</param>
        /// <param name="fileSize">Size of the file.</param>
        /// <returns></returns>
        private static Rectangle GetFileRectange(Rectangle rect, int fileSize)
        {
            // The image is later stretched as extents are 8 pages wide
            var widthHeightRatio = rect.Width / (rect.Height * 6D);

            var height = (int)(Math.Ceiling(Math.Sqrt((double)fileSize) / widthHeightRatio));
            var width = (int)(Math.Ceiling(Math.Sqrt((double)fileSize) * widthHeightRatio));

            // Adjust so the stride won't have any padding bytes
            width = (width + 4) - (width % 4);

            return new Rectangle(0, 0, width, height);
        }

        /// <summary>
        /// Writes the allocation bytes directly to the bitmap data
        /// </summary>
        /// <param name="bitmap">The bitmap.</param>
        /// <param name="allocation">The allocation structure.</param>
        /// <param name="startPage">The start page.</param>
        /// <param name="fileSize">Size of the file.</param>
        /// <param name="colour">The layer colour.</param>
        private static void AddAllocationToBitmap(Bitmap bitmap, bool[] allocation, PageAddress startPage, int fileSize, Color colour)
        {
            var startExtent = startPage.PageId / 8;

            var bytesPerExtent = 3; // R, G, B bytes

            var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                                                    ImageLockMode.ReadWrite,
                                                    bitmap.PixelFormat);

            var ptr = bitmapData.Scan0;

            var values = new byte[fileSize * bytesPerExtent];

            // Copy the bitmap data into a managed array
            Marshal.Copy(ptr, values, 0, fileSize * bytesPerExtent);

            var extent = startExtent;

            for (var i = startExtent * bytesPerExtent;
                 i < Math.Min(values.Length, (startExtent + allocation.Length) * bytesPerExtent);
                 i += bytesPerExtent)
            {
                // If it's allocated set the B G R values to the colour (else leave them as is)
                if (allocation[extent - startExtent])
                {
                    values[i] = colour.B;
                    values[i + 1] = colour.G;
                    values[i + 2] = colour.R;
                }

                extent++;
            }

            // Copy the managed array back into the bitmap data
            Marshal.Copy(values, 0, ptr, fileSize * bytesPerExtent);

            bitmap.UnlockBits(bitmapData);
        }
    }
}
