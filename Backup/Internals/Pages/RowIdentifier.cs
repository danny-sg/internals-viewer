using System;
using System.Globalization;
using System.Text;

namespace InternalsViewer.Internals.Pages
{
    /// <summary>
    /// Row Identifier (RID)
    /// </summary>
    public struct RowIdentifier
    {
        private PageAddress pageAddress;
        private int slotId;
        public const int Size = sizeof(Int16) + sizeof(Int16) + sizeof(Int32);

        /// <summary>
        /// Initializes a new instance of the <see cref="RowIdentifier"/> struct.
        /// </summary>
        /// <param name="address">The address.</param>
        public RowIdentifier(byte[] address)
        {
            this.pageAddress = new PageAddress(BitConverter.ToInt16(address, 4), BitConverter.ToInt32(address, 0));
            this.slotId = BitConverter.ToInt16(address, 6);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RowIdentifier"/> struct.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="slot">The slot.</param>
        public RowIdentifier(PageAddress page, int slot)
        {
            this.pageAddress = page;
            this.slotId = slot;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RowIdentifier"/> struct.
        /// </summary>
        /// <param name="fileId">The file id.</param>
        /// <param name="pageId">The page id.</param>
        /// <param name="slot">The slot.</param>
        public RowIdentifier(int fileId, int pageId, int slot)
        {
            this.pageAddress = new PageAddress(fileId, pageId);
            this.slotId = slot;
        }

        /// <summary>
        /// Parses a specified address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns></returns>
        public static RowIdentifier Parse(string address)
        {
            int fileId;
            int pageId;
            Int16 slot = 0;

            bool parsed;

            StringBuilder sb = new StringBuilder(address);
            sb.Replace("(", string.Empty);
            sb.Replace(")", string.Empty);
            sb.Replace(",", ":");

            string[] splitAddress = sb.ToString().Split(@":".ToCharArray());

            if (splitAddress.Length < 2)
            {
                throw new ArgumentException("Invalid format");
            }

            parsed = true & int.TryParse(splitAddress[0], out fileId);
            parsed = parsed & int.TryParse(splitAddress[1], out pageId);

            if (splitAddress.Length > 2)
            {
                parsed = parsed & short.TryParse(splitAddress[2], out slot);
            }

            if (parsed)
            {
                return new RowIdentifier(new PageAddress(fileId, pageId), slot);
            }
            else
            {
                throw new ArgumentException("Invalid format");
            }
        }

        /// <summary>
        /// Returns the fully qualified type name of this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> containing a fully qualified type name.
        /// </returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentCulture, "({0}:{1}:{2})",
                                 this.pageAddress.FileId,
                                 this.pageAddress.PageId,
                                 this.slotId);
        }

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
        /// Gets or sets the slot id.
        /// </summary>
        /// <value>The slot id.</value>
        public int SlotId
        {
            get { return this.slotId; }
            set { this.slotId = value; }
        }

    }
}
