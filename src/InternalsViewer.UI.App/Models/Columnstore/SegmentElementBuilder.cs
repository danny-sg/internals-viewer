using System.Collections.Generic;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.UI.App.Models.Columnstore;

/// <summary>
/// Turns a parsed blob into the regions the structure table navigates
/// </summary>
/// <remarks>
/// Only regions are listed. Field level detail comes from the markers the parser records, which the marker tree
/// shows against whichever region the hex window sits on.
/// </remarks>
public static class SegmentElementBuilder
{
    private const string HeaderGroup = "Header";

    public static List<SegmentElement> Build(SegmentBlob blob)
    {
        var elements = new List<SegmentElement>
        {
            new()
            {
                Name = HeaderGroup,
                Offset = 0,
                Size = blob.PrologueSize
            }
        };

        elements.Add(new SegmentElement
        {
            Name = "Bookmark Array",
            Offset = blob.BookmarkArrayOffset,
            Size = blob.BookmarkCount * SegmentBlob.EntrySize
        });

        if (blob.IsStoreByValue)
        {
            elements.Add(new SegmentElement
            {
                Name = "Value Store",
                Offset = blob.ValueStoreOffset,
                Size = blob.Data.Length - blob.ValueStoreOffset
            });

            return elements;
        }

        elements.Add(new SegmentElement
        {
            Name = "RLE Array",
            Offset = blob.RleArrayOffset,
            Size = blob.RleArrayCount * SegmentBlob.EntrySize
        });

        elements.Add(new SegmentElement
        {
            Name = "Bit Pack Array",
            Offset = blob.BitpackArrayOffset,
            Size = blob.BitpackUnitCount * SegmentBlob.EntrySize
        });

        return elements;
    }

}
