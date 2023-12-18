using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Engine.Pages;

/// <summary>
/// Allocation Page containing an allocation bitmap (IAM, GAM, SGAM, DCM, BCM)
/// </summary>
/// <remarks>
/// Allocation Pages are used to track the allocation of pages and extents within a database file
/// </remarks>
public class AllocationPage : Page
{
    /// <summary>
    /// Interval between allocation pages
    /// </summary>
    /// <remarks>
    /// 1 bit per extent representing allocation status of the extent
    /// 
    /// 63,904 bits = 7,988 bytes = 1 page (8,192 bytes) less page header/overhead.
    /// </remarks>
    public const int AllocationInterval = 63904;

    public const int AllocationArrayOffset = 194;

    public const int SinglePageSlotOffset = 142;

    public const int StartPageOffset = 136;

    public const int SlotCount = 8;

    public const int FirstGamPage = 2;

    public const int FirstSgamPage = 3;

    public const int FirstDcmPage = 6;

    public const int FirstBcmPage = 7;

    /// <summary>
    /// Allocation bitmap
    /// </summary>
    public bool[] AllocationMap { get; set; } = new bool[AllocationInterval];

    /// <summary>
    /// Single page slots collection (IAM pages only).
    /// </summary>
    public List<PageAddress> SinglePageSlots { get; set; } = new();

    /// <summary>
    /// Gets or sets the start page.
    /// </summary>
    public PageAddress StartPage { get; set; }
}