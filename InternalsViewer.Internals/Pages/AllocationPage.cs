using System;
using System.Collections;
using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;

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

        /// <summary>
        /// Gets the allocation map.
        /// </summary>
        /// <value>The allocation map.</value>
        public bool[] AllocationMap { get; } = new bool[64000];

        /// <summary>
        /// Gets the single page slots collection.
        /// </summary>
        /// <value>The single page slots collection.</value>
        public List<PageAddress> SinglePageSlots { get; } = new List<PageAddress>();

        /// <summary>
        /// Gets or sets the start page.
        /// </summary>
        /// <value>The start page.</value>
        public PageAddress StartPage { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AllocationPage"/> class.
        /// </summary>
        /// <param name="page">The page.</param>
        public AllocationPage(Page page)
        {
            Header = page.Header;
            PageData = page.PageData;
            PageAddress = page.PageAddress;
            DatabaseId = page.DatabaseId;

            LoadPage(true);
            LoadAllocationMap();
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

            switch (Header.PageType)
            {
                case PageType.Bcm:
                case PageType.Dcm:
                case PageType.Iam:
                case PageType.Sgam:
                case PageType.Gam:
                    LoadAllocationMap();
                    break;
                default:
                    throw new InvalidOperationException(Header.PageType + " is not an allocation page");
            }
        }

        public AllocationPage(string connectionString, string database, PageAddress pageAddress)
            : base(connectionString, database, pageAddress)
        {
            LoadAllocationMap();
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
            LoadAllocationMap();
        }

        /// <summary>
        /// Loads the allocation map.
        /// </summary>
        private void LoadAllocationMap()
        {
            var allocationData = new byte[8000];
            int allocationArrayOffset;

            switch (Header.PageType)
            {
                case PageType.Gam:
                case PageType.Sgam:
                case PageType.Dcm:
                case PageType.Bcm:

                    StartPage = new PageAddress(Header.PageAddress.FileId, 0);
                    allocationArrayOffset = AllocationArrayOffset;
                    break;

                case PageType.Iam:

                    allocationArrayOffset = AllocationArrayOffset;

                    LoadIamHeader();
                    LoadSinglePageSlots();

                    break;

                default:
                    return;
            }

            Array.Copy(PageData,
                       allocationArrayOffset,
                       allocationData,
                       0,
                       allocationData.Length - (Header.SlotCount * 2));

            var bitArray = new BitArray(allocationData);

            bitArray.CopyTo(AllocationMap, 0);
        }

        /// <summary>
        /// Loads the IAM header.
        /// </summary>
        private void LoadIamHeader()
        {
            var pageAddress = new byte[6];

            Array.Copy(PageData, StartPageOffset, pageAddress, 0, 6);

            StartPage = new PageAddress(pageAddress);
        }

        /// <summary>
        /// Loads the single page slots.
        /// </summary>
        private void LoadSinglePageSlots()
        {
            var offset = SinglePageSlotOffset;

            for (var i = 0; i < 8; i++)
            {
                var pageAddress = new byte[6];

                Array.Copy(PageData, offset, pageAddress, 0, 6);

                SinglePageSlots.Add(new PageAddress(pageAddress));

                offset += 6;
            }
        }
    }
}
