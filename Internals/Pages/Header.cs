
namespace InternalsViewer.Internals.Pages
{
    /// <summary>
    /// Page Header
    /// </summary>
    public class Header
    {
        private string allocationUnit;
        private long allocationUnitId;
        private string flagBits;
        private int freeCount;
        private int freeData;
        private int indexId;
        private int level;
        private LogSequenceNumber lsn;
        private int minLen;
        private PageAddress nextPage;
        private long objectId;
        private PageAddress pageAddress;
        private PageType pageType;
        private string pageTypeName;
        private long partitionId;
        private PageAddress previousPage;
        private int reservedCount;
        private int slotCount;
        private long tornBits;
        private int xactReservedCount;

        /// <summary>
        /// Gets or sets the page address.
        /// </summary>
        /// <value>The page address.</value>
        public PageAddress PageAddress
        {
            get { return this.pageAddress; }
            set { this.pageAddress = value; }
        }

        /// <summary>
        /// Gets or sets the next page.
        /// </summary>
        /// <value>The next page.</value>
        public PageAddress NextPage
        {
            get { return this.nextPage; }
            set { this.nextPage = value; }
        }

        /// <summary>
        /// Gets or sets the previous page.
        /// </summary>
        /// <value>The previous page.</value>
        public PageAddress PreviousPage
        {
            get { return this.previousPage; }
            set { this.previousPage = value; }
        }

        /// <summary>
        /// Gets or sets the type of the page.
        /// </summary>
        /// <value>The type of the page.</value>
        public PageType PageType
        {
            get { return this.pageType; }
            set { this.pageType = value; }
        }

        /// <summary>
        /// Gets or sets the allocation unit id.
        /// </summary>
        /// <value>The allocation unit id.</value>
        public long AllocationUnitId
        {
            get { return this.allocationUnitId; }
            set { this.allocationUnitId = value; }
        }

        /// <summary>
        /// Gets or sets the page index level.
        /// </summary>
        /// <value>The page index level.</value>
        public int Level
        {
            get { return this.level; }
            set { this.level = value; }
        }

        /// <summary>
        /// Gets or sets the index id.
        /// </summary>
        /// <value>The index id.</value>
        public int IndexId
        {
            get { return this.indexId; }
            set { this.indexId = value; }
        }

        /// <summary>
        /// Gets or sets the slot count.
        /// </summary>
        /// <value>The slot count.</value>
        public int SlotCount
        {
            get { return this.slotCount; }
            set { this.slotCount = value; }
        }

        /// <summary>
        /// Gets or sets the free count value.
        /// </summary>
        /// <value>The free count value.</value>
        public int FreeCount
        {
            get { return this.freeCount; }
            set { this.freeCount = value; }
        }

        /// <summary>
        /// Gets or sets the free data value.
        /// </summary>
        /// <value>The free data value.</value>
        public int FreeData
        {
            get { return this.freeData; }
            set { this.freeData = value; }
        }

        /// <summary>
        /// Gets or sets the min len value.
        /// </summary>
        /// <value>The min len value.</value>
        public int MinLen
        {
            get { return this.minLen; }
            set { this.minLen = value; }
        }

        /// <summary>
        /// Gets or sets the reserved count value.
        /// </summary>
        /// <value>The reserved count value.</value>
        public int ReservedCount
        {
            get { return this.reservedCount; }
            set { this.reservedCount = value; }
        }

        /// <summary>
        /// Gets or sets the xact reserved count value.
        /// </summary>
        /// <value>The xact reserved count value.</value>
        public int XactReservedCount
        {
            get { return this.xactReservedCount; }
            set { this.xactReservedCount = value; }
        }

        /// <summary>
        /// Gets or sets the torn bits value.
        /// </summary>
        /// <value>The torn bits value.</value>
        public long TornBits
        {
            get { return this.tornBits; }
            set { this.tornBits = value; }
        }

        /// <summary>
        /// Gets or sets the flag bits.
        /// </summary>
        /// <value>The flag bits.</value>
        public string FlagBits
        {
            get { return this.flagBits; }
            set { this.flagBits = value; }
        }

        /// <summary>
        /// Gets or sets the object id.
        /// </summary>
        /// <value>The object id.</value>
        public long ObjectId
        {
            get { return this.objectId; }
            set { this.objectId = value; }
        }

        /// <summary>
        /// Gets or sets the partition id.
        /// </summary>
        /// <value>The partition id.</value>
        public long PartitionId
        {
            get { return this.partitionId; }
            set { this.partitionId = value; }
        }

        /// <summary>
        /// Gets or sets the LSN.
        /// </summary>
        /// <value>The LSN.</value>
        public LogSequenceNumber Lsn
        {
            get { return this.lsn; }
            set { this.lsn = value; }
        }

        /// <summary>
        /// Gets or sets the allocation unit.
        /// </summary>
        /// <value>The allocation unit.</value>
        public string AllocationUnit
        {
            get { return this.allocationUnit; }
            set { this.allocationUnit = value; }
        }

        /// <summary>
        /// Gets or sets the name of the page type.
        /// </summary>
        /// <value>The name of the page type.</value>
        public string PageTypeName
        {
            get { return this.pageTypeName; }
            set { this.pageTypeName = value; }
        }
    }
}
