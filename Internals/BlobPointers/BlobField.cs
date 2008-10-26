using System.Collections.ObjectModel;
using System.Text;

namespace InternalsViewer.Internals.BlobPointers
{
    public abstract class BlobField : Field
    {
        private byte[] data;
        private Collection<BlobChildLink> links;
        private BlobFieldType pointerType;
        private int timestamp;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlobField"/> class.
        /// </summary>
        /// <param name="data">The data.</param>
        public BlobField(byte[] data)
        {
            this.data = data;
            this.pointerType = (BlobFieldType)data[0];
            this.LoadLinks();
        }

        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        /// <value>The timestamp.</value>
        public int Timestamp
        {
            get { return this.timestamp; }
            set { this.timestamp = value; }
        }

        /// <summary>
        /// Gets or sets the links.
        /// </summary>
        /// <value>The links.</value>
        public Collection<BlobChildLink> Links
        {
            get { return this.links; }
            set { this.links = value; }
        }

        /// <summary>
        /// Gets or sets the data.
        /// </summary>
        /// <value>The data.</value>
        public byte[] Data
        {
            get { return this.data; }
            set { this.data = value; }
        }

        /// <summary>
        /// Gets or sets the type of the blob pointer.
        /// </summary>
        /// <value>The type of the blob pointer.</value>
        public BlobFieldType PointerType
        {
            get { return this.pointerType; }
            set { this.pointerType = value; }
        }

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(this.Timestamp.ToString());

            foreach (BlobChildLink b in this.Links)
            {
                sb.AppendLine(b.ToString());
            }

            return sb.ToString();
        }

        /// <summary>
        /// Loads the links.
        /// </summary>
        protected virtual void LoadLinks()
        {
        }
    }
}
