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

    public static IEnumerable<Marker> ArrayMarkers(DictionaryBlob? blob)
    {
        switch (blob)
        {
            case NumericDictionary { ValueCount: > 0, ElementSize: > 0 } numeric:
                yield return Region("Value Array",
                                    ItemType.ValueArrayRegion,
                                    NumericDictionary.HeaderSize,
                                    numeric.ValueCount * numeric.ElementSize,
                                    $"({numeric.ValueCount} Entries)");

                break;

            case StringDictionary strings:
                var pageSizes = StringDictionary.HandleArrayOffset + (strings.HandleCount * strings.HandleSize);

                var pages = pageSizes + (strings.PageCount * PageSizeBytes);

                yield return Region("Handle Array",
                                    ItemType.HandleArrayRegion,
                                    StringDictionary.HandleArrayOffset,
                                    strings.HandleCount * strings.HandleSize,
                                    $"({strings.HandleCount} Handles)");

                yield return Region("Page Size Array",
                                    ItemType.PageSizeArrayRegion,
                                    pageSizes,
                                    strings.PageCount * PageSizeBytes,
                                    $"({strings.PageCount} Pages)");

                yield return Region("Pages",
                                    ItemType.StringPageRegion,
                                    pages,
                                    Math.Max(0, blob.Data.Length - pages),
                                    $"({strings.PageCount} Pages)");

                break;
        }
    }

    private static Marker Region(string name, ItemType type, int offset, int size, string value)
        => MarkerBuilder.CreateMarker(name, type, offset, size, value);

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

            var marker = MarkerBuilder.CreateMarker("Entry",
                                                    ItemType.StringEntryValue,
                                                    extent.Offset,
                                                    extent.PrefixLength + extent.Length,
                                                    entry.Value);

            marker.Children =
            [
                MarkerBuilder.CreateMarker("Entry Length",
                                           ItemType.StringEntryLength,
                                           extent.Offset,
                                           extent.PrefixLength,
                                           $"{extent.Length} bytes"),
                MarkerBuilder.CreateMarker("Entry Value",
                                           ItemType.StringEntryValue,
                                           extent.ValueOffset,
                                           extent.Length,
                                           entry.Value)
            ];

            yield return marker;

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
        if (handle is null || blob is not StringDictionary strings)
        {
            yield break;
        }

        var marker = MarkerBuilder.CreateMarker("Handle",
                                                ItemType.DictionaryHandle,
                                                handle.HandleOffset,
                                                strings.HandleSize,
                                                $"Page {handle.Page}, Offset {handle.Offset}");

        marker.Children =
        [
            MarkerBuilder.CreateMarker("Offset",
                                       ItemType.DictionaryHandleOffset,
                                       handle.HandleOffset,
                                       HandleFieldBytes,
                                       $"{handle.Offset}"),
            MarkerBuilder.CreateMarker("Page",
                                       ItemType.DictionaryHandlePage,
                                       handle.HandleOffset + HandleFieldBytes,
                                       HandleFieldBytes,
                                       $"{handle.Page}")
        ];

        yield return marker;
    }
}
