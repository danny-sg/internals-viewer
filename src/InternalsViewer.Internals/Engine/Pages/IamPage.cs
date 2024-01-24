using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Annotations;

namespace InternalsViewer.Internals.Engine.Pages;

public class IamPage : AllocationPage
{
    public const int StartPageOffset = 136;

    public int SequenceNumber { get; set; }

    public byte Status { get; set; }

    public int ObjectId { get; set; }

    public int IndexId { get; set; }

    public int PageCount { get; set; }

    /// <summary>
    /// Single page slots collection (IAM pages only).
    /// </summary>
    public PageAddress[] SinglePageSlots { get; set; } = new PageAddress[8];

    [DataStructureItem(DataStructureItemType.PageAddress, "Single Page Slot 0")]
    public PageAddress SinglePageSlot0 => SinglePageSlots[0];

    [DataStructureItem(DataStructureItemType.PageAddress, "Single Page Slot 1")]
    public PageAddress SinglePageSlot1 => SinglePageSlots[1];

    [DataStructureItem(DataStructureItemType.PageAddress, "Single Page Slot 2")]
    public PageAddress SinglePageSlot2 => SinglePageSlots[2];

    [DataStructureItem(DataStructureItemType.PageAddress, "Single Page Slot 3")]
    public PageAddress SinglePageSlot3 => SinglePageSlots[3];

    [DataStructureItem(DataStructureItemType.PageAddress, "Single Page Slot 4")]
    public PageAddress SinglePageSlot4 => SinglePageSlots[4];

    [DataStructureItem(DataStructureItemType.PageAddress, "Single Page Slot 5")]
    public PageAddress SinglePageSlot5 => SinglePageSlots[5];

    [DataStructureItem(DataStructureItemType.PageAddress, "Single Page Slot 6")]
    public PageAddress SinglePageSlot6 => SinglePageSlots[6];

    [DataStructureItem(DataStructureItemType.PageAddress, "Single Page Slot 7")]
    public PageAddress SinglePageSlot7 => SinglePageSlots[7];

    /// <summary>
    /// Start page for the allocation
    /// </summary>
    [DataStructureItem(DataStructureItemType.PageAddress, "IAM Start Page")]
    public PageAddress StartPage { get; set; } = PageAddress.Empty;
}