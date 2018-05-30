
using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Pages
{
    /// <summary>
    /// Page Header
    /// </summary>
    public class Header
    {
        /// <summary>
        /// Gets or sets the page address.
        /// </summary>
        /// <value>The page address.</value>
        public PageAddress PageAddress { get; set; }

        /// <summary>
        /// Gets or sets the next page.
        /// </summary>
        /// <value>The next page.</value>
        public PageAddress NextPage { get; set; }

        /// <summary>
        /// Gets or sets the previous page.
        /// </summary>
        /// <value>The previous page.</value>
        public PageAddress PreviousPage { get; set; }

        /// <summary>
        /// Gets or sets the type of the page.
        /// </summary>
        /// <value>The type of the page.</value>
        public PageType PageType { get; set; }

        /// <summary>
        /// Gets or sets the allocation unit id.
        /// </summary>
        /// <value>The allocation unit id.</value>
        public long AllocationUnitId { get; set; }

        /// <summary>
        /// Gets or sets the page index level.
        /// </summary>
        /// <value>The page index level.</value>
        public int Level { get; set; }

        /// <summary>
        /// Gets or sets the index id.
        /// </summary>
        /// <value>The index id.</value>
        public int IndexId { get; set; }

        /// <summary>
        /// Gets or sets the slot count.
        /// </summary>
        /// <value>The slot count.</value>
        public int SlotCount { get; set; }

        /// <summary>
        /// Gets or sets the free count value.
        /// </summary>
        /// <value>The free count value.</value>
        public int FreeCount { get; set; }

        /// <summary>
        /// Gets or sets the free data value.
        /// </summary>
        /// <value>The free data value.</value>
        public int FreeData { get; set; }

        /// <summary>
        /// Gets or sets the min len value.
        /// </summary>
        /// <value>The min len value.</value>
        public int MinLen { get; set; }

        /// <summary>
        /// Gets or sets the reserved count value.
        /// </summary>
        /// <value>The reserved count value.</value>
        public int ReservedCount { get; set; }

        /// <summary>
        /// Gets or sets the xact reserved count value.
        /// </summary>
        /// <value>The xact reserved count value.</value>
        public int XactReservedCount { get; set; }

        /// <summary>
        /// Gets or sets the torn bits value.
        /// </summary>
        /// <value>The torn bits value.</value>
        public long TornBits { get; set; }

        /// <summary>
        /// Gets or sets the flag bits.
        /// </summary>
        /// <value>The flag bits.</value>
        public string FlagBits { get; set; }

        /// <summary>
        /// Gets or sets the object id.
        /// </summary>
        /// <value>The object id.</value>
        public long ObjectId { get; set; }

        /// <summary>
        /// Gets or sets the partition id.
        /// </summary>
        /// <value>The partition id.</value>
        public long PartitionId { get; set; }

        /// <summary>
        /// Gets or sets the LSN.
        /// </summary>
        /// <value>The LSN.</value>
        public LogSequenceNumber Lsn { get; set; }

        /// <summary>
        /// Gets or sets the allocation unit.
        /// </summary>
        /// <value>The allocation unit.</value>
        public string AllocationUnit { get; set; }

        /// <summary>
        /// Gets or sets the name of the page type.
        /// </summary>
        /// <value>The name of the page type.</value>
        public string PageTypeName { get; set; }
    }
}
