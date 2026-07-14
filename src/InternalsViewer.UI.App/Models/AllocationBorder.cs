using System.Collections.Generic;
using System.Drawing;

namespace InternalsViewer.UI.App.Models;

/// <summary>Whether an <see cref="AllocationBorder"/>'s cells are pages or extents</summary>
public enum AllocationBorderScope
{
    Page,
    Extent
}

/// <summary>
/// A contiguous run of cells (page ids or extent ids), <c>FromCell</c> .. <c>ToCell</c> inclusive, and the window it is
/// active for, <c>StartUs</c> .. <c>EndUs</c>
/// </summary>
/// <remarks>
/// A range, not a single cell, because an object's allocation is mostly contiguous extents — one range stands in for
/// thousands of pages. A single page is just a range of one (<c>FromCell == ToCell</c>).
/// </remarks>
public readonly record struct TimedRange(int FromCell, int ToCell, long StartUs, long EndUs);

/// <summary>
/// A group of time-gated cell ranges to outline on the allocation map as a single Tetris-piece perimeter in one colour
/// </summary>
/// <remarks>
/// Each range carries its own active window, so as the playhead moves the outline traces exactly the cells live at that
/// instant (a lock appearing/releasing changes the shape in step with the timeline). The map expands only the live
/// ranges and draws the OUTSIDE edge of that set (marching-squares — an edge is drawn where a live cell borders a
/// non-live cell), never per-cell borders, so contiguous live cells merge and non-contiguous ones read as separate
/// shapes. General-purpose (a lock is one use — a page lock outlines its page while held, an object lock outlines all
/// its allocated pages while held).
/// </remarks>
public sealed record AllocationBorder(AllocationBorderScope Scope,
                                      short FileId,
                                      Color Colour,
                                      IReadOnlyList<TimedRange> Cells);
