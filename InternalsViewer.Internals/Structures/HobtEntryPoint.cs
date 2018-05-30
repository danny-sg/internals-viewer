using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Structures
{
    /// <summary>
    /// The entry point pages for a HOBT
    /// </summary>
    public struct HobtEntryPoint
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HobtEntryPoint"/> struct.
        /// </summary>
        /// <param name="firstIam">The first iam.</param>
        /// <param name="rootPage">The root page.</param>
        /// <param name="firstPage">The first page.</param>
        /// <param name="partitionNumber">The partition number.</param>
        public HobtEntryPoint(PageAddress firstIam, PageAddress rootPage, PageAddress firstPage, int partitionNumber)
        {
            FirstIam = firstIam;
            RootPage = rootPage;
            FirstPage = firstPage;
            PartitionNumber = partitionNumber;
        }

        /// <summary>
        /// Gets or sets the first IAM page address.
        /// </summary>
        /// <value>The first iam.</value>
        /// <remarks>
        /// The first page in the HOBTs allocation IAM chain
        /// </remarks>
        public PageAddress FirstIam { get; set; }

        /// <summary>
        /// Gets or sets the root page address.
        /// </summary>
        /// <value>The root page.</value>
        /// <remarks>
        /// The root page of the b-tree (index)
        /// </remarks>
        public PageAddress RootPage { get; set; }

        /// <summary>
        /// Gets or sets the first page address;
        /// </summary>
        /// <value>The first page.</value>
        /// <remarks>
        /// The first page at the leaf/heap level
        /// </remarks>
        public PageAddress FirstPage { get; set; }

        /// <summary>
        /// Gets or sets the partition number.
        /// </summary>
        /// <value>The partition number.</value>
        public int PartitionNumber { get; set; }
    }
}
