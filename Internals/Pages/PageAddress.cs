using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace InternalsViewer.Internals.Pages
{
    /// <summary>
    /// Page Address that gives a unique address for a page in a database
    /// </summary>
    [Serializable]
    public struct PageAddress : IEquatable<PageAddress>, IComparable<PageAddress>
    {
        public static readonly PageAddress Empty = new PageAddress();
        private int fileId;
        private int pageId;

        /// <summary>
        /// Initializes a new instance of the <see cref="PageAddress"/> struct.
        /// </summary>
        /// <param name="address">The page address in a valid format.</param>
        public PageAddress(string address)
        {
            try
            {
                PageAddress pageAddress = Parse(address);
                this.fileId = pageAddress.fileId;
                this.pageId = pageAddress.pageId;
            }
            catch
            {
                this.fileId = 0;
                this.pageId = 0;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PageAddress"/> struct.
        /// </summary>
        /// <param name="fileId">The file id.</param>
        /// <param name="pageId">The page id.</param>
        public PageAddress(int fileId, int pageId)
        {
            this.fileId = fileId;
            this.pageId = pageId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PageAddress"/> struct.
        /// </summary>
        /// <param name="address">The page address in internal 6 byte form.</param>
        public PageAddress(byte[] address)
        {
            this.pageId = BitConverter.ToInt32(address, 0);
            this.fileId = BitConverter.ToInt16(address, 4);
        }

        /// <summary>
        /// Parses the specified page address.
        /// </summary>
        /// <param name="address">The page address.</param>
        /// <returns></returns>
        public static PageAddress Parse(string address)
        {
            Regex bytePattern = new Regex(@"[0-9a-fA-F]{4}[\x3A][0-9a-fA-F]{8}$");

            if (bytePattern.IsMatch(address))
            {
                return ParseBytes(address);
            }

            int fileId;
            int pageId;

            bool parsed;

            StringBuilder sb = new StringBuilder(address);
            sb.Replace("(", string.Empty);
            sb.Replace(")", string.Empty);
            sb.Replace(",", ":");

            string[] splitAddress = sb.ToString().Split(@":".ToCharArray());

            if (splitAddress.Length != 2)
            {
                throw new ArgumentException("Invalid Format");
            }

            parsed = true & int.TryParse(splitAddress[0], out fileId);
            parsed = parsed & int.TryParse(splitAddress[1], out pageId);

            if (parsed)
            {
                return new PageAddress(fileId, pageId);
            }
            else
            {
                throw new ArgumentException("Invalid Format");
            }
        }

        /// <summary>
        /// Parses an address from hex bytes.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns></returns>
        private static PageAddress ParseBytes(string address)
        {
            int fileId;
            int pageId;

            string[] bytes = address.Split(new char[] { ':' });

            int.TryParse(bytes[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out fileId);
            int.TryParse(bytes[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out pageId);

            return new PageAddress(fileId, pageId);
        }

        /// <summary>
        /// Returns the fully qualified type name of this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> containing a fully qualified type name.
        /// </returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentCulture, "({0}:{1})", this.fileId, this.pageId);
        }

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        /// <param name="obj">Another object to compare to.</param>
        /// <returns>
        /// true if <paramref name="obj"/> and this instance are the same type and represent the same value; otherwise, false.
        /// </returns>
        public override bool Equals(object obj)
        {
            return obj is PageAddress && this == (PageAddress)obj;
        }

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="address1">The address1.</param>
        /// <param name="address2">The address2.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator ==(PageAddress address1, PageAddress address2)
        {
            return address1.PageId == address2.PageId && address1.FileId == address2.FileId;
        }

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator !=(PageAddress x, PageAddress y)
        {
            return !(x == y);
        }

        /// <summary>
        /// Equalses the specified page address.
        /// </summary>
        /// <param name="pageAddress">The page address.</param>
        /// <returns></returns>
        public bool Equals(PageAddress pageAddress)
        {
            return this.fileId == pageAddress.fileId && this.pageId == pageAddress.pageId;
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        public override int GetHashCode()
        {
            return this.fileId + 29 * this.pageId;
        }

        /// <summary>
        /// Compares the current object with another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// A 32-bit signed integer that indicates the relative order of the objects being compared. 
        /// The return value has the following meanings: Value Meaning Less than zero This object is less than the <paramref name="other"/> 
        /// parameter. 
        /// Zero This object is equal to <paramref name="other"/>. Greater than zero This object is greater than <paramref name="other"/>.
        /// </returns>
        public int CompareTo(PageAddress other)
        {
            return (this.FileId.CompareTo(other.FileId) * 9999999) + this.PageId.CompareTo(other.PageId);
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
        /// Gets or sets the page id.
        /// </summary>
        /// <value>The page id.</value>
        public int PageId
        {
            get { return this.pageId; }
            set { this.pageId = value; }
        }
    }
}
