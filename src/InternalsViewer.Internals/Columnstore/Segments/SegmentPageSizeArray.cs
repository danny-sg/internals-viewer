using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Segments;

public sealed class SegmentPageSizeArray : DataStructure
{
    public const int Size = 12;

    public int Offset { get; set; }

    [DataStructureItem(ItemType.PageSizeArraySubLobType)]
    public SubLobType SubLobType { get; set; }

    [DataStructureItem(ItemType.PageSizeArrayElementSize)]
    public int ElementSize { get; set; }

    [DataStructureItem(ItemType.PageSizeArrayElementCount)]
    public int ElementCount { get; set; }

    public int[] PageSizes { get; set; } = [];

    public int DataOffset => Offset + Size;

    public int TotalSize => Size + (ElementCount * ElementSize);

    public void Mark()
    {
        MarkProperty(nameof(SubLobType), Offset, 4);
        MarkProperty(nameof(ElementSize), Offset + 0x04, 4);
        MarkProperty(nameof(ElementCount), Offset + 0x08, 4);

        if (ElementSize <= 0)
        {
            return;
        }

        for (var i = 0; i < PageSizes.Length; i++)
        {
            MarkValue(ItemType.PageSizeArrayData,
                      $"Page {i}",
                      PageSizes[i],
                      DataOffset + (i * ElementSize),
                      ElementSize);
        }
    }
}
