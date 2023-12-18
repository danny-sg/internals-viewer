using System.Globalization;

namespace InternalsViewer.Internals.Engine.Address;

/// <summary>
/// Page Address that gives a unique address for a page in a database
/// </summary>
public record struct PageAddress(short FileId, int PageId)
{
    public static readonly PageAddress Empty = new();

    /// <summary>
    /// Size of a Page Address is 6 bytes (2 byte File Id + 4 byte Page Id)
    /// </summary>
    public const int Size = sizeof(short) + sizeof(int);

    /// <summary>
    /// File Id for the Page Address
    /// </summary>
    public short FileId { get; } = FileId;

    /// <summary>
    /// Page Id for the Page Address
    /// </summary>
    public int PageId { get; } = PageId;

    /// <summary>
    /// Extent (block of 8 pages) for the Page Address
    /// </summary>
    public readonly int Extent => (PageId - 1) / 8;

    public override string ToString()
    {
        return string.Format(CultureInfo.CurrentCulture, "({0}:{1})", FileId, PageId);
    }
}