using System;
using System.Globalization;
using System.Text;

namespace InternalsViewer.Internals.Engine.Address
{
    /// <summary>
    /// Row Identifier (RID)
    /// </summary>
    public struct RowIdentifier
    {
        public const int Size = sizeof(short) + sizeof(short) + sizeof(int);

        /// <summary>
        /// Gets or sets the page address.
        /// </summary>
        /// <value>The page address.</value>
        public PageAddress PageAddress { get; set; }

        /// <summary>
        /// Gets or sets the slot id.
        /// </summary>
        /// <value>The slot id.</value>
        public int SlotId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RowIdentifier"/> struct.
        /// </summary>
        /// <param name="address">The address.</param>
        public RowIdentifier(byte[] address)
        {
            PageAddress = new PageAddress(BitConverter.ToInt16(address, 4), BitConverter.ToInt32(address, 0));
            SlotId = BitConverter.ToInt16(address, 6);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RowIdentifier"/> struct.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="slot">The slot.</param>
        public RowIdentifier(PageAddress page, int slot)
        {
            PageAddress = page;
            SlotId = slot;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RowIdentifier"/> struct.
        /// </summary>
        /// <param name="fileId">The file id.</param>
        /// <param name="pageId">The page id.</param>
        /// <param name="slot">The slot.</param>
        public RowIdentifier(int fileId, int pageId, int slot)
        {
            PageAddress = new PageAddress(fileId, pageId);
            SlotId = slot;
        }

        /// <summary>
        /// Parses a specified address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns></returns>
        public static RowIdentifier Parse(string address)
        {
            short slot = 0;

            var sb = new StringBuilder(address);
            sb.Replace("(", string.Empty);
            sb.Replace(")", string.Empty);
            sb.Replace(",", ":");

            var splitAddress = sb.ToString().Split(@":".ToCharArray());

            if (splitAddress.Length < 2)
            {
                throw new ArgumentException("Invalid format");
            }

            var parsed = true & int.TryParse(splitAddress[0], out var fileId);
            parsed = parsed & int.TryParse(splitAddress[1], out var pageId);

            if (splitAddress.Length > 2)
            {
                parsed = parsed & short.TryParse(splitAddress[2], out slot);
            }

            if (parsed)
            {
                return new RowIdentifier(new PageAddress(fileId, pageId), slot);
            }

            throw new ArgumentException("Invalid format");
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
                                 PageAddress.FileId,
                                 PageAddress.PageId,
                                 SlotId);
        }
    }
}
