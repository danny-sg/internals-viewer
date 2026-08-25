using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Columnstore.Dictionary;

namespace InternalsViewer.UI.App.Services.Markers;

public static class DictionaryMarkerBuilder
{
    private const int MaxMarkedEntries = 10;

    private const int HandleFieldBytes = 4;

    public const int PageSizeBytes = 4;

    private const int DictionaryHeaderSize = 12;

    public static List<Marker> GroupHeader(List<Marker> markers)
    {
        var loose = markers.Where(m => m.Children.Count == 0 && m.EndPosition < DictionaryHeaderSize).ToList();

        if (loose.Count == 0)
        {
            return markers;
        }

        var from = loose.Min(m => m.StartPosition);

        var section = MarkerBuilder.CreateMarker("Dictionary Header",
                                                 ItemType.SegmentHeaderSection,
                                                 from,
                                                 loose.Max(m => m.EndPosition) - from + 1,
                                                 string.Empty);

        section.Children = new ObservableCollection<Marker>(loose);

        return [section, .. markers.Except(loose)];
    }

    public static List<Marker> Window(IEnumerable<Marker> markers, int start, int length)
    {
        var windowed = new List<Marker>();

        var end = start + length - 1;

        foreach (var marker in markers)
        {
            // Clipped rather than dropped, a coded entry running well past whatever the window happens to hold
            var from = Math.Max(marker.StartPosition, start);

            var to = Math.Min(marker.EndPosition, end);

            if (marker.StartPosition < 0 || to < from)
            {
                marker.StartPosition = -1;
                marker.EndPosition = -1;
            }
            else
            {
                marker.StartPosition = from - start;
                marker.EndPosition = to - start;
            }

            windowed.Add(marker);
        }

        return windowed;
    }

    /// <summary>
    /// The arrays the headers describe, which are data rather than fields and so are not marked as the blob is parsed
    /// </summary>
    /// <remarks>
    /// A dictionary runs to tens of thousands of entries, so past a handful the run is marked as one region instead.
    /// Marking every entry would flood the tree, and the entry a reader wants is the one they select in the grid.
    /// </remarks>
    public static IEnumerable<Marker> ArrayMarkers(DictionaryBlob? blob)
    {
        switch (blob)
        {
            case NumericDictionary { ValueCount: > 0, ElementSize: > 0 } numeric:
                yield return MarkerBuilder.CreateMarker("Value Array",
                                                        ItemType.DictionaryValue,
                                                        NumericDictionary.HeaderSize,
                                                        numeric.ValueCount * numeric.ElementSize,
                                                        $"({numeric.ValueCount} Entries)");

                break;

            case StringDictionary strings:
                foreach (var marker in HandleMarkers(strings))
                {
                    yield return marker;
                }

                foreach (var marker in MarkRegion("Page Size",
                                                  ItemType.DictionaryPageSize,
                                                  StringDictionary.HandleArrayOffset
                                                  + (strings.HandleCount * strings.HandleSize),
                                                  strings.PageCount,
                                                  PageSizeBytes,
                                                  i => $"{strings.PageSizes[i]} bytes"))
                {
                    yield return marker;
                }

                break;
        }
    }

    /// <summary>
    /// Handles carry two fields, so each one that is marked on its own opens to show them
    /// </summary>
    private static IEnumerable<Marker> HandleMarkers(StringDictionary strings)
    {
        var markers = MarkRegion("Handle",
                                 ItemType.DictionaryHandle,
                                 StringDictionary.HandleArrayOffset,
                                 strings.HandleCount,
                                 strings.HandleSize,
                                 _ => string.Empty);

        if (strings.HandleCount > MaxMarkedEntries)
        {
            return markers;
        }

        return markers.Select((marker, index) =>
        {
            var handle = strings.Handles[index];

            marker.Children =
            [
                MarkerBuilder.CreateMarker("Offset",
                                           ItemType.DictionaryHandleOffset,
                                           marker.StartPosition,
                                           HandleFieldBytes,
                                           $"{handle.Offset}"),
                MarkerBuilder.CreateMarker("Page",
                                           ItemType.DictionaryHandlePage,
                                           marker.StartPosition + HandleFieldBytes,
                                           HandleFieldBytes,
                                           $"{handle.Page}")
            ];

            return marker;
        });
    }

    /// <summary>
    /// One marker per entry while there are few enough to read, and one over the whole run once there are not
    /// </summary>
    private static IEnumerable<Marker> MarkRegion(string name,
                                                  ItemType type,
                                                  int offset,
                                                  int count,
                                                  int elementSize,
                                                  Func<int, string> describe)
    {
        if (count <= 0 || elementSize <= 0)
        {
            yield break;
        }

        if (count > MaxMarkedEntries)
        {
            yield return MarkerBuilder.CreateMarker($"{name} Array",
                                                    type,
                                                    offset,
                                                    count * elementSize,
                                                    $"({count} Entries)");

            yield break;
        }

        for (var i = 0; i < count; i++)
        {
            yield return MarkerBuilder.CreateMarker($"{name} {i}",
                                                    type,
                                                    offset + (i * elementSize),
                                                    elementSize,
                                                    describe(i));
        }
    }

    /// <summary>
    /// The selected entry as it sits in its page, which the parser cannot mark because it depends on the selection
    /// </summary>
    /// <remarks>
    /// An uncompressed entry is a length prefix and the bytes it counts. A coded one has no byte boundaries of its
    /// own, so the whole run of words its bits fall in is marked instead and the bit walk shows the detail.
    /// </remarks>
    public static IEnumerable<Marker> EntryMarkers(DictionaryBlob? blob,
                                                   DictionaryPageSummary? page,
                                                   DictionaryEntryDetail? entry,
                                                   IReadOnlyList<HuffmanDecodeStep> steps)
    {
        if (entry is null)
        {
            yield break;
        }

        if (blob is NumericDictionary)
        {
            yield return MarkerBuilder.CreateMarker("Value",
                                                    ItemType.DictionaryValue,
                                                    entry.ValueOffset,
                                                    entry.ValueSize,
                                                    entry.Value);

            yield break;
        }

        if (page is null || blob is not StringDictionary strings)
        {
            yield break;
        }

        var handle = strings.Handles[entry.Index];

        if (page.Page is UncompressedStringPage uncompressed)
        {
            var extent = uncompressed.GetExtent(handle.Offset);

            yield return MarkerBuilder.CreateMarker("Entry Length",
                                                    ItemType.StringEntryLength,
                                                    extent.Offset,
                                                    extent.PrefixLength,
                                                    $"{extent.Length} bytes");

            yield return MarkerBuilder.CreateMarker("Entry Value",
                                                    ItemType.StringEntryValue,
                                                    extent.ValueOffset,
                                                    extent.Length,
                                                    entry.Value);

            yield break;
        }

        if (page.Huffman is null || steps.Count == 0)
        {
            yield break;
        }

        var contentStart = page.Offset + HuffmanStringPage.DataOffset;

        var firstBit = steps[0].BitOffset;

        var lastBit = steps[^1].BitOffset + steps[^1].BitLength;

        var start = contentStart + (firstBit / 16 * 2);

        var end = contentStart + (((lastBit - 1) / 16 * 2) + 2);

        yield return MarkerBuilder.CreateMarker("Coded Entry",
                                                ItemType.StringEntryCode,
                                                start,
                                                end - start,
                                                $"{lastBit - firstBit} bits from bit {firstBit}");
    }

    public static IEnumerable<Marker> SelectedHandleMarkers(DictionaryBlob? blob, DictionaryHandleDetail? handle)
    {
        if (handle is null || blob is not StringDictionary)
        {
            yield break;
        }

        yield return MarkerBuilder.CreateMarker("Offset",
                                                ItemType.DictionaryHandleOffset,
                                                handle.HandleOffset,
                                                HandleFieldBytes,
                                                $"{handle.Offset}");

        yield return MarkerBuilder.CreateMarker("Page",
                                                ItemType.DictionaryHandlePage,
                                                handle.HandleOffset + HandleFieldBytes,
                                                HandleFieldBytes,
                                                $"{handle.Page}");
    }
}
