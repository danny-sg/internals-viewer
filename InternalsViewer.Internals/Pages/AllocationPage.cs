using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Pages;

/// <summary>
/// Allocation Page containing an allocation bitmap
/// 
/// Allocation Pages are used to track the allocation of pages and extents within a database file
/// </summary>
public class AllocationPage : Page
{
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
    public bool[] AllocationMap { get; set; } = new bool[Database.AllocationInterval];

    /// <summary>
    /// Single page slots collection (IAM pages only).
    /// </summary>
    public List<PageAddress> SinglePageSlots { get; set; } = new();

    /// <summary>
    /// Gets or sets the start page.
    /// </summary>
    public PageAddress StartPage { get; set; }
}