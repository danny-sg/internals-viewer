using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals
{
    /// <summary>
    /// An Allocation structure represented by a collection of allocation pages separated by an interval
    /// </summary>
    public class Allocation
    {
        private readonly List<AllocationPage> pages = new List<AllocationPage>();
        private int fileId;
        private int interval;
        private bool multiFile;
        private List<PageAddress> singlePageSlots = new List<PageAddress>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Allocation"/> class.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="pageAddress">The page address.</param>
        public Allocation(Database database, PageAddress pageAddress)
        {
            this.FileId = pageAddress.FileId;
            this.MultiFile = false;
            this.BuildChain(database, pageAddress);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Allocation"/> class.
        /// </summary>
        /// <param name="page">The page.</param>
        public Allocation(AllocationPage page)
        {
            this.FileId = page.PageAddress.FileId;
            this.MultiFile = false;
            this.pages.Add(page);
            this.interval = Database.AllocationInterval;
            this.singlePageSlots.AddRange(page.SinglePageSlots);
        }

        /// <summary>
        /// Check if a specific extent is allocated
        /// </summary>
        /// <param name="extent">The extent.</param>
        /// <param name="allocationFileId">The allocation file id.</param>
        /// <returns></returns>
        public virtual bool Allocated(int extent, int allocationFileId)
        {
            return this.pages[(extent * 8) / this.interval].AllocationMap[extent % ((this.interval / 8) + 1)];
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
            AllocationPage page = new AllocationPage(database, pageAddress);

            if (page.Header.PageType == PageType.Iam)
            {
                throw new ArgumentException();
            }

            this.pages.Add(page);
            this.singlePageSlots.AddRange(page.SinglePageSlots);

            this.interval = Database.AllocationInterval;

            int pageCount = (int)Math.Ceiling(database.FileSize(pageAddress.FileId) / (decimal)this.interval);

            if (pageCount > 1)
            {
                for (int i = 1; i < pageCount; i++)
                {
                    this.pages.Add(new AllocationPage(database,
                                                 new PageAddress(pageAddress.FileId,
                                                                 pageAddress.PageId + (i * this.interval))));
                }
            }
        }

        /// <summary>
        /// Refreshes this instance.
        /// </summary>
        public void Refresh()
        {
            this.singlePageSlots.Clear();

            foreach (AllocationPage page in this.Pages)
            {
                page.Refresh();
                this.singlePageSlots.AddRange(page.SinglePageSlots);
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
        public List<AllocationPage> Pages
        {
            get { return this.pages; }
        }

        /// <summary>
        /// Gets or sets the single page slots.
        /// </summary>
        /// <value>The single page slots.</value>
        public List<PageAddress> SinglePageSlots
        {
            get { return this.singlePageSlots; }
            set { this.singlePageSlots = value; }
        }

        /// <summary>
        /// Gets or sets the file id.
        /// </summary>
        /// <value>The file id.</value>
        public int FileId
        {
            get { return this.fileId; }
            set { this.fileId = value; }
        }

        /// <summary>
        /// Determines if the Allocation spans multiple files
        /// </summary>
        public bool MultiFile
        {
            get { return this.multiFile; }
            set { this.multiFile = value; }
        }
        #endregion
    }
}
