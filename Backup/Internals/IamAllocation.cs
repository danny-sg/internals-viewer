using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals
{
    /// <summary>
    /// An IAM allocation structure
    /// </summary>
    /// <remarks>
    /// This is a subclass of Allocation as the BuildChain and Allocated method is overriden with a different method
    /// </remarks>
    public class IamAllocation : Allocation
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IamAllocation"/> class.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="pageAddress">The page address.</param>
        public IamAllocation(Database database, PageAddress pageAddress)
            : base(database, pageAddress)
        {
            this.MultiFile = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IamAllocation"/> class.
        /// </summary>
        /// <param name="page">The page.</param>
        public IamAllocation(AllocationPage page)
            : base(page)
        {
            this.MultiFile = true;
        }

        /// <summary>
        /// Check is a specific extent is allocated
        /// </summary>
        /// <param name="extent">The extent.</param>
        /// <param name="fileId">The file id.</param>
        /// <returns></returns>
        public override bool Allocated(int extent, int fileId)
        {
            AllocationPage page = Pages.Find(delegate(AllocationPage p)
            {
                return p.StartPage.FileId == fileId &&
                       extent >= (p.StartPage.PageId / 8) &&
                       extent <= ((p.StartPage.PageId + Database.AllocationInterval) / 8);
            });

            if (page == null)
            {
                return false;
            }

            return page.AllocationMap[extent - (page.StartPage.PageId / 8)];
        }

        /// <summary>
        /// Builds an allocation chain based on linkage through the headers.
        /// </summary>
        /// <param name="database"></param>
        /// <param name="pageAddress"></param>
        protected override void BuildChain(Database database, PageAddress pageAddress)
        {
            AllocationPage page = new AllocationPage(database, pageAddress);
            Pages.Add(page);
            SinglePageSlots.AddRange(page.SinglePageSlots);

            if (page.Header.NextPage != PageAddress.Empty)
            {
                this.BuildChain(database, page.Header.NextPage);
            }
        }
    }
}
