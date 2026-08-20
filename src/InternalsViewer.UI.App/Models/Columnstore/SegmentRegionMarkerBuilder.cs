using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Segments;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Interfaces.Annotations;
using System.Linq;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Services.Markers;

namespace InternalsViewer.UI.App.Models.Columnstore;

/// <summary>
/// Builds markers for the region on show, covering the entries of it that fall inside the hex window
/// </summary>
/// <remarks>
/// The arrays run to hundreds of thousands of entries, so a marker per entry can only ever be built for the window
/// on screen. Only the region on show is marked, so scrolling into a neighbouring region leaves its bytes unmarked
/// rather than mixing two regions' markers together.
/// </remarks>
public static class SegmentRegionMarkerBuilder
{
    public static List<Marker> Build(SegmentBlob blob, SegmentRegion region, int windowStart, int windowLength)
        => region switch
        {
            SegmentRegion.Header => Windowed(MarkerBuilder.BuildMarkers(blob), windowStart, windowLength),
            SegmentRegion.Bookmarks => [.. BookmarkHeader(blob), .. BuildBookmarks(blob, windowStart, windowLength)],
            SegmentRegion.RleArray => [.. RleHeader(blob), .. BuildRleEntries(blob, windowStart, windowLength)],
            SegmentRegion.BitpackArray => [.. BitpackHeader(blob),
                                           .. BuildBitpackUnits(blob, windowStart, windowLength)],
            SegmentRegion.ValueStore => ValueStoreMarkers(blob, windowStart, windowLength),
            _ => []
        };

    /// <summary>
    /// The store header and page size array, with the header of every page the window holds
    /// </summary>
    private static List<Marker> ValueStoreMarkers(SegmentBlob blob, int windowStart, int windowLength)
    {
        if (blob.ValueStore is not { } store)
        {
            return [];
        }

        var sources = new List<IDataStructure> { store };

        sources.AddRange(store.Pages);

        return Windowed(sources.SelectMany(MarkerBuilder.BuildMarkers), windowStart, windowLength);
    }

    /// <summary>
    /// Header fields describing the region, shown for context above its entries
    /// </summary>
    /// <remarks>
    /// The fields live in the header rather than the region, so they sit outside the window the hex view is showing
    /// and carry no position within it. The header tab is where their bytes can be seen.
    /// </remarks>
    private static IEnumerable<Marker> RleHeader(SegmentBlob blob)
    {
        yield return ContextMarker("RLE Entry Count", ItemType.RleArrayCount, $"{blob.RleEntryCount}");
        yield return ContextMarker("RLE Entry Size", ItemType.RleEntrySize, $"{blob.RleEntryBytes} bytes");
        yield return ContextMarker("Lob Type", ItemType.SegmentStructureType, $"{blob.StructureType.ToString().SplitCamelCase()} ({(int)blob.StructureType})");
    }

    private static IEnumerable<Marker> BookmarkHeader(SegmentBlob blob)
    {
        yield return ContextMarker("Bookmark Count", ItemType.BookmarkCount, $"{blob.BookmarkCount}");
        yield return ContextMarker("Bookmark Distance", ItemType.BookmarkDistance, $"{blob.BookmarkDistance} rows");
    }

    private static IEnumerable<Marker> BitpackHeader(SegmentBlob blob)
    {
        yield return ContextMarker("Bit Pack Entry Size", ItemType.BitpackEntrySize, $"{blob.BitpackEntrySize} bits");
        yield return ContextMarker("Values Per Unit", ItemType.BitpackEntrySize, $"{blob.Bitpack.ValuesPerUnit}");
        yield return ContextMarker("Bit Pack Unit Count", ItemType.BitpackUnitCount, $"{blob.BitpackUnitCount}");
        yield return ContextMarker("Bit Pack Min Id", ItemType.BitpackMinId, $"{blob.BitpackMinId}");
    }

    private static Marker ContextMarker(string name, ItemType type, string value)
    {
        var marker = MarkerBuilder.CreateMarker(name, type, 0, 0, value);

        marker.StartPosition = -1;
        marker.EndPosition = -1;

        return marker;
    }

    private static List<Marker> BuildBookmarks(SegmentBlob blob, int windowStart, int windowLength)
    {
        var markers = new List<Marker>();

        var (first, last) = GetEntryRange(blob.BookmarkArrayOffset,
                                          SegmentBlob.EntrySize,
                                          blob.BookmarkCount,
                                          windowStart,
                                          windowLength);

        for (var i = first; i <= last; i++)
        {
            var offset = blob.BookmarkArrayOffset + (i * SegmentBlob.EntrySize) - windowStart;

            var bookmark = blob.Bookmarks[i];

            markers.Add(Create($"Bookmark Index {i} Position",
                               ItemType.BookmarkPosition,
                               offset,
                               4,
                               $"{bookmark.Position} (entry {bookmark.GetRleEntryIndex(blob.RleEntryBytes)})"));

            markers.Add(Create($"Bookmark Index {i} End Row",
                               ItemType.BookmarkEndRow,
                               offset + 4,
                               4,
                               $"{bookmark.EndRow}"));
        }

        return markers;
    }

    private static List<Marker> BuildRleEntries(SegmentBlob blob, int windowStart, int windowLength)
    {
        var markers = new List<Marker>();

        var entryBytes = blob.RleEntryBytes;

        var valueSize = entryBytes / 2;

        var (first, last) = GetEntryRange(blob.RleArrayOffset,
                                          entryBytes,
                                          blob.RleEntryCount,
                                          windowStart,
                                          windowLength);

        for (var i = first; i <= last; i++)
        {
            var offset = blob.RleArrayOffset + (i * entryBytes) - windowStart;

            var entry = blob.RleEntries[i];

            if (entry.IsBitpacked)
            {
                markers.Add(Create($"RLE Index {i} Bit Pack Index",
                                   ItemType.RleBitpackIndex,
                                   offset,
                                   valueSize,
                                   $"{entry.BitpackIndex}"));
            }
            else
            {
                markers.Add(Create($"RLE Index {i} Value",
                                   ItemType.RleValue,
                                   offset,
                                   valueSize,
                                   entry.IsTerminator ? "Terminator" : $"{entry.Value}"));
            }

            markers.Add(Create($"RLE Index {i} Count", ItemType.RleCount, offset + valueSize, 4, $"{entry.Count}"));
        }

        return markers;
    }

    /// <summary>
    /// One marker per packed unit, the values inside it being narrower than a byte and left to the bit ruler
    /// </summary>
    /// <remarks>
    /// A packed value rarely starts on a byte boundary, so marking values rather than units would give overlapping
    /// byte ranges that the hex view can neither colour nor look up unambiguously.
    /// </remarks>
    private static List<Marker> BuildBitpackUnits(SegmentBlob blob, int windowStart, int windowLength)
    {
        var markers = new List<Marker>();

        var perUnit = blob.Bitpack.ValuesPerUnit;

        var (first, last) = GetEntryRange(blob.BitpackArrayOffset,
                                          BitpackArray.UnitBytes,
                                          blob.BitpackUnitCount,
                                          windowStart,
                                          windowLength);

        for (var i = first; i <= last; i++)
        {
            var offset = blob.BitpackArrayOffset + (i * BitpackArray.UnitBytes) - windowStart;

            markers.Add(Create($"Unit {i}",
                               ItemType.BitpackUnit,
                               offset,
                               BitpackArray.UnitBytes,
                               perUnit switch
                               {
                                   0 => "Empty",
                                   1 => "(Value)",
                                   _ => $"({perUnit} Values)"
                               }));
        }

        return markers;
    }

    /// <summary>
    /// Entries that intersect the window, clamped to the ones the array actually has
    /// </summary>
    private static (int First, int Last) GetEntryRange(int arrayOffset,
                                                       int entrySize,
                                                       int entryCount,
                                                       int windowStart,
                                                       int windowLength)
    {
        if (entryCount <= 0 || entrySize <= 0)
        {
            return (0, -1);
        }

        var first = Math.Max(0, (windowStart - arrayOffset) / entrySize);

        var last = Math.Min(entryCount - 1, (windowStart + windowLength - arrayOffset) / entrySize);

        return (first, last);
    }

    /// <summary>
    /// Rebases a structure's fields onto the window, keeping the ones it does not reach
    /// </summary>
    /// <remarks>
    /// A field outside the window keeps its place in the tree and loses its position, the same as a field marked for
    /// context. Dropping it instead empties a tree the moment the window moves elsewhere, which is what the entries
    /// of an array want but never what a fixed set of fields wants.
    /// </remarks>
    public static List<Marker> Window(IEnumerable<Marker> markers, int windowStart, int windowLength)
    {
        var windowed = new List<Marker>();

        var windowEnd = windowStart + windowLength - 1;

        foreach (var marker in markers)
        {
            var start = Math.Max(marker.StartPosition, windowStart);

            var end = Math.Min(marker.EndPosition, windowEnd);

            if (marker.StartPosition < 0 || end < start)
            {
                marker.StartPosition = -1;
                marker.EndPosition = -1;
            }
            else
            {
                marker.StartPosition = start - windowStart;
                marker.EndPosition = end - windowStart;
            }

            windowed.Add(marker);
        }

        return windowed;
    }

    /// <summary>
    /// Rebases the fields onto the window, clipped to the part of each the window actually holds
    /// </summary>
    /// <remarks>
    /// Clipped rather than dropped because a field can be longer than the window - a page payload runs to thousands
    /// of bytes - and dropping it would leave the largest regions the only ones never marked.
    /// </remarks>
    private static List<Marker> Windowed(IEnumerable<Marker> markers, int windowStart, int windowLength)
    {
        var windowed = new List<Marker>();

        var windowEnd = windowStart + windowLength - 1;

        foreach (var marker in markers)
        {
            if (marker.StartPosition < 0)
            {
                continue;
            }

            var start = Math.Max(marker.StartPosition, windowStart);

            var end = Math.Min(marker.EndPosition, windowEnd);

            if (end < start)
            {
                continue;
            }

            marker.StartPosition = start - windowStart;
            marker.EndPosition = end - windowStart;

            windowed.Add(marker);
        }

        return windowed;
    }

    private static Marker Create(string name, ItemType type, int offset, int size, string value)
        => MarkerBuilder.CreateMarker(name, type, offset, size, value);
}
