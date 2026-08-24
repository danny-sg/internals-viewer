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
                Size = blob.Header.PrologueSize
            }
        };

        elements.Add(new SegmentElement
        {
            Name = "Bookmark Array",
            Offset = blob.Header.BookmarkArrayOffset,
            Size = blob.Header.BookmarkCount * SegmentBlob.EntrySize
        });

        if (blob.Header.IsStoreByValue)
        {
            elements.Add(new SegmentElement
            {
                Name = "Variable Length Data",
                Offset = blob.Header.VariableLengthDataOffset,
                Size = blob.Data.Length - blob.Header.VariableLengthDataOffset
            });

            return elements;
        }

        elements.Add(new SegmentElement
        {
            Name = "RLE Array",
            Offset = blob.Header.RleArrayOffset,
            Size = blob.Header.RleArrayCount * SegmentBlob.EntrySize
        });

        elements.Add(new SegmentElement
        {
            Name = "Bit Pack Array",
            Offset = blob.Header.BitpackArrayOffset,
            Size = blob.Header.BitpackUnitCount * SegmentBlob.EntrySize
        });

        return elements;
    }

}
