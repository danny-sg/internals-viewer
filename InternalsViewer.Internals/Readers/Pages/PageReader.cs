using System.Globalization;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.Readers.Headers;

namespace InternalsViewer.Internals.Readers.Pages
{
    /// <summary>
    /// Abstract class for reading pages
    /// </summary>
    public abstract class PageReader
    {
        protected PageReader()
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PageReader"/> class.
        /// </summary>
        /// <param name="pageAddress">The page address.</param>
        /// <param name="databaseId">The database id.</param>
        protected PageReader(PageAddress pageAddress, int databaseId)
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

        protected static int ReadData(string currentRow, int offset, byte[] data)
        {
            var currentData = currentRow.Substring(20, 44).Replace(" ", string.Empty);

            for (var i = 0; i < currentData.Length; i += 2)
            {
                var byteString = currentData.Substring(i, 2);

                if (!byteString.Contains("†") && !byteString.Contains(".") && offset < Page.Size)
                {
                    if (byte.TryParse(byteString,
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out data[offset]))
                    {
                        offset++;
                    }
                }
            }

            return offset;
        }

        protected HeaderReader HeaderReader { get; set; }

        /// <summary>
        /// Loads the page  header.
        /// </summary>
        /// <returns></returns>
        public bool LoadHeader()
        {
            var header = new Header();

            var parsed = HeaderReader.LoadHeader(header);

            Header = header;

            return parsed;
        }
    }
}
