using System;
using System.Collections;
using System.Collections.Generic;

namespace InternalsViewer.Internals.Pages
{
    /// <summary>
    /// Allocation Page containing an allocation bitmap
    /// </summary>
    public class AllocationPage : Page
    {
        public const int AllocationArrayOffset = 194;
        public const int SinglePageSlotOffset = 142;
        public const int StartPageOffset = 136;

        private readonly bool[] allocationMap = new bool[64000];
        private readonly List<PageAddress> singlePageSlots = new List<PageAddress>();
        private PageAddress startPage;

        /// <summary>
        /// Initializes a new instance of the <see cref="AllocationPage"/> class.
        /// </summary>
        /// <param name="page">The page.</param>
        public AllocationPage(Page page)
        {
            this.Header = page.Header;
            this.PageData = page.PageData;
            this.PageAddress = page.PageAddress;
            this.DatabaseId = page.DatabaseId;

            LoadPage(true);
            this.LoadAllocationMap();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AllocationPage"/> class.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="address">The page address.</param>
        public AllocationPage(Database database, PageAddress address)
            : base(database, address)
        {
            PageAddress = address;

            switch (this.Header.PageType)
            {
                case PageType.Bcm:
                case PageType.Dcm:
                case PageType.Iam:
                case PageType.Sgam:
                case PageType.Gam:


                    this.LoadAllocationMap();
                    break;
                default:
                    throw new InvalidOperationException(this.Header.PageType + " is not an allocation page");
                    break;


            }

        }

        public AllocationPage(string connectionString, string database, PageAddress pageAddress)
            : base(connectionString, database, pageAddress)
        {
            this.LoadAllocationMap();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AllocationPage"/> class.
        /// </summary>
        public AllocationPage()
        {
            Header = new Header();
            Header.PageType = PageType.None;
        }

        /// <summary>
        /// Refresh the Page and allocation bitmap
        /// </summary>
        public override void Refresh()
        {
            base.Refresh();
            this.LoadAllocationMap();
        }

        /// <summary>
        /// Loads the allocation map.
        /// </summary>
        private void LoadAllocationMap()
        {
            byte[] allocationData = new byte[8000];
            int allocationArrayOffset;

            switch (Header.PageType)
            {
                case PageType.Gam:
                case PageType.Sgam:
                case PageType.Dcm:
                case PageType.Bcm:

                    this.StartPage = new PageAddress(Header.PageAddress.FileId, 0);
                    allocationArrayOffset = AllocationArrayOffset;
                    break;

                case PageType.Iam:

                    allocationArrayOffset = AllocationArrayOffset;

                    this.LoadIamHeader();
                    this.LoadSinglePageSlots();

                    break;

                default:
                    return;
            }

            Array.Copy(PageData,
                       allocationArrayOffset,
                       allocationData,
                       0,
                       allocationData.Length - (Header.SlotCount * 2));

            BitArray bitArray = new BitArray(allocationData);

            bitArray.CopyTo(this.allocationMap, 0);
        }

        /// <summary>
        /// Loads the IAM header.
        /// </summary>
        private void LoadIamHeader()
        {
            byte[] pageAddress = new byte[6];

            Array.Copy(PageData, StartPageOffset, pageAddress, 0, 6);

            this.startPage = new PageAddress(pageAddress);
        }

        /// <summary>
        /// Loads the single page slots.
        /// </summary>
        private void LoadSinglePageSlots()
        {
            int slotOffset = SinglePageSlotOffset;

            for (int i = 0; i < 8; i++)
            {
                byte[] pageAddress = new byte[6];

                Array.Copy(PageData, slotOffset, pageAddress, 0, 6);

                this.singlePageSlots.Add(new PageAddress(pageAddress));

                slotOffset += 6;
            }
        }

        #region Properties

        /// <summary>
        /// Gets the allocation map.
        /// </summary>
        /// <value>The allocation map.</value>
        public bool[] AllocationMap
        {
            get { return this.allocationMap; }
        }

        /// <summary>
        /// Gets the single page slots collection.
        /// </summary>
        /// <value>The single page slots collection.</value>
        public List<PageAddress> SinglePageSlots
        {
            get { return this.singlePageSlots; }
        }

        /// <summary>
        /// Gets or sets the start page.
        /// </summary>
        /// <value>The start page.</value>
        public PageAddress StartPage
        {
            get { return this.startPage; }
            set { this.startPage = value; }
        }

        #endregion
    }
}
