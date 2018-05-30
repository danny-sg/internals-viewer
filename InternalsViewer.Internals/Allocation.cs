using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals
{
    /// <summary>
    /// An Allocation structure represented by a collection of allocation pages separated by an interval
    /// </summary>
    public class Allocation
    {
        private int interval;

        /// <summary>
        /// Initializes a new instance of the <see cref="Allocation"/> class.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="pageAddress">The page address.</param>
        public Allocation(Database database, PageAddress pageAddress)
        {
            FileId = pageAddress.FileId;
            MultiFile = false;
            BuildChain(database, pageAddress);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Allocation"/> class.
        /// </summary>
        /// <param name="page">The page.</param>
        public Allocation(AllocationPage page)
        {
            FileId = page.PageAddress.FileId;
            MultiFile = false;
            Pages.Add(page);
            interval = Database.AllocationInterval;
            SinglePageSlots.AddRange(page.SinglePageSlots);
        }

        /// <summary>
        /// Check if a specific extent is allocated
        /// </summary>
        /// <param name="extent">The extent.</param>
        /// <param name="allocationFileId">The allocation file id.</param>
        /// <returns></returns>
        public virtual bool Allocated(int extent, int allocationFileId)
        {
            return Pages[(extent * 8) / interval].AllocationMap[extent % ((interval / 8) + 1)];
        }

        /// <summary>
        /// Checks the allocation status or an extent
        /// </summary>
        /// <param name="targetExtent">The target extent.</param>
        /// <param name="fileId">The file id.</param>
        /// <param name="invert">if set to <c>true</c> [invert].</param>
        /// <param name="chain">The chain.</param>
        /// <returns></returns>
        public static bool CheckAllocationStatus(int targetExtent, int fileId, bool invert, Allocation chain)
        {
            return (!invert
                    && chain.Allocated(targetExtent, fileId)
                    && (fileId == chain.FileId || chain.MultiFile)
                   )
                   ||
                   (invert
                    && !chain.Allocated(targetExtent, fileId)
                    && (fileId == chain.FileId || chain.MultiFile)
                   );
        }

        /// <summary>
        /// Builds the allocation chain following an interval
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="pageAddress">The page address.</param>
        protected virtual void BuildChain(Database database, PageAddress pageAddress)
        {
            var page = new AllocationPage(database, pageAddress);

            if (page.Header.PageType == PageType.Iam)
            {
                //throw new ArgumentException();
            }

            Pages.Add(page);
            SinglePageSlots.AddRange(page.SinglePageSlots);

            interval = Database.AllocationInterval;

            var pageCount = (int)Math.Ceiling(database.FileSize(pageAddress.FileId) / (decimal)interval);

            if (pageCount > 1)
            {
                for (var i = 1; i < pageCount; i++)
                {
                    Pages.Add(new AllocationPage(database,
                                                 new PageAddress(pageAddress.FileId,
                                                                 pageAddress.PageId + (i * interval))));
                }
            }
        }

        /// <summary>
        /// Refreshes this instance.
        /// </summary>
        public void Refresh()
        {
            SinglePageSlots.Clear();

            foreach (var page in Pages)
            {
                page.Refresh();
                SinglePageSlots.AddRange(page.SinglePageSlots);
            }
        }

        /// <summary>
        /// Returns a page address for the allocation page that corresponds to a page address and allocation page type
        /// </summary>
        /// <param name="pageAddress">The page address.</param>
        /// <param name="pageType">Type of the page.</param>
        /// <returns></returns>
        public static PageAddress AllocationPageAddress(PageAddress pageAddress, PageType pageType)
        {
            int pageId;

            switch (pageType)
            {
                case PageType.Pfs:

                    if (pageAddress.PageId < Database.PfsInterval)
                    {
                        return new PageAddress(pageAddress.FileId, 1);
                    }
                    else
                    {
                        pageId = (pageAddress.PageId / Database.PfsInterval) * Database.PfsInterval;

                        return new PageAddress(pageAddress.FileId, pageId);
                    }

                default:

                    if (pageAddress.PageId < Database.AllocationInterval)
                    {
                        pageId = 2;

                        switch (pageType)
                        {
                            case PageType.Sgam:

                                pageId += 1;
                                break;

                            case PageType.Dcm:

                                pageId += 4;
                                break;

                            case PageType.Bcm:

                                pageId += 5;
                                break;
                        }
                    }
                    else
                    {
                        pageId = (pageAddress.PageId / Database.AllocationInterval) * Database.AllocationInterval;

                        switch (pageType)
                        {
                            case PageType.Sgam:

                                pageId += 1;
                                break;

                            case PageType.Dcm:

                                pageId += 6;
                                break;

                            case PageType.Bcm:

                                pageId += 7;
                                break;
                        }
                    }

                    return new PageAddress(pageAddress.FileId, pageId);
            }
        }

        #region Properties

        /// <summary>
        /// Gets the pages.
        /// </summary>
        /// <value>The pages.</value>
        public List<AllocationPage> Pages { get; } = new List<AllocationPage>();

        /// <summary>
        /// Gets or sets the single page slots.
        /// </summary>
        /// <value>The single page slots.</value>
        public List<PageAddress> SinglePageSlots { get; set; } = new List<PageAddress>();

        /// <summary>
        /// Gets or sets the file id.
        /// </summary>
        /// <value>The file id.</value>
        public int FileId { get; set; }

        /// <summary>
        /// Determines if the Allocation spans multiple files
        /// </summary>
        public bool MultiFile { get; set; }

        #endregion
    }
}
