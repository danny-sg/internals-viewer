using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.PageIO
{
    /// <summary>
    /// Abstract class for reading pages
    /// </summary>
    public abstract class PageReader
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PageReader"/> class.
        /// </summary>
        /// <param name="pageAddress">The page address.</param>
        /// <param name="databaseId">The database id.</param>
        public PageReader(PageAddress pageAddress, int databaseId)
        {
            PageAddress = pageAddress;
            DatabaseId = databaseId;
        }

        /// <summary>
        /// Gets or sets the page data.
        /// </summary>
        /// <value>The data.</value>
        public byte[] Data { get; set; }

        /// <summary>
        /// Gets or sets the page header.
        /// </summary>
        /// <value>The page header.</value>
        internal Header Header { get; set; }

        /// <summary>
        /// Gets or sets the page address.
        /// </summary>
        /// <value>The page address.</value>
        public PageAddress PageAddress { get; set; }

        /// <summary>
        /// Gets or sets the database id.
        /// </summary>
        /// <value>The database id.</value>
        public int DatabaseId { get; set; }

        /// <summary>
        /// Loads this instance.
        /// </summary>
        public abstract void Load();

        /// <summary>
        /// Loads the header.
        /// </summary>
        /// <returns></returns>
        public abstract bool LoadHeader();
    }
}
