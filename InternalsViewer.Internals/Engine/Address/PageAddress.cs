using System;
using System.Globalization;

namespace InternalsViewer.Internals.Engine.Address;

/// <summary>
/// Page Address that gives a unique address for a page in a database
/// </summary>
public record struct PageAddress(short FileId, int PageId)
{
    public static readonly PageAddress Empty = new();

    public const int Size = sizeof(int) + sizeof(short);

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