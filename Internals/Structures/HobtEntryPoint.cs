using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Structures
{
    /// <summary>
    /// The entry point pages for a HOBT
    /// </summary>
    public struct HobtEntryPoint
    {
        private PageAddress firstIam;
        private PageAddress rootPage;
        private PageAddress firstPage;
        private int partitionNumber;

        /// <summary>
        /// Initializes a new instance of the <see cref="HobtEntryPoint"/> struct.
        /// </summary>
        /// <param name="firstIam">The first iam.</param>
        /// <param name="rootPage">The root page.</param>
        /// <param name="firstPage">The first page.</param>
        /// <param name="partitionNumber">The partition number.</param>
        public HobtEntryPoint(PageAddress firstIam, PageAddress rootPage, PageAddress firstPage, int partitionNumber)
        {
            this.firstIam = firstIam;
            this.rootPage = rootPage;
            this.firstPage = firstPage;
            this.partitionNumber = partitionNumber;
        }

        /// <summary>
        /// Gets or sets the first IAM page address.
        /// </summary>
        /// <value>The first iam.</value>
        /// <remarks>
        /// The first page in the HOBTs allocation IAM chain
        /// </remarks>
        public PageAddress FirstIam
        {
            get { return firstIam; }
            set { firstIam = value; }
        }

        /// <summary>
        /// Gets or sets the root page address.
        /// </summary>
        /// <value>The root page.</value>
        /// <remarks>
        /// The root page of the b-tree (index)
        /// </remarks>
        public PageAddress RootPage
        {
            get { return rootPage; }
            set { rootPage = value; }
        }

        /// <summary>
        /// Gets or sets the first page address;
        /// </summary>
        /// <value>The first page.</value>
        /// <remarks>
        /// The first page at the leaf/heap level
        /// </remarks>
        public PageAddress FirstPage
        {
            get { return firstPage; }
            set { firstPage = value; }
        }

        /// <summary>
        /// Gets or sets the partition number.
        /// </summary>
        /// <value>The partition number.</value>
        public int PartitionNumber
        {
            get { return partitionNumber; }
            set { partitionNumber = value; }
        }
    }
}
