using System.Collections.ObjectModel;
using System.Text;
using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.BlobPointers
{
    public abstract class BlobField : Field
    {
        private byte[] data;
        private List<BlobChildLink> links;
        private BlobFieldType pointerType;
        private int timestamp;
        private int offset;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlobField"/> class.
        /// </summary>
        /// <param name="data">The data.</param>
        public BlobField(byte[] data, int offset)
        {
            this.data = data;
            this.pointerType = (BlobFieldType)data[0];
            this.Offset = offset;

            this.Mark("PointerType", offset, sizeof(byte));
            
            this.LoadLinks();
        }

        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        /// <value>The timestamp.</value>
        [MarkAttribute("Timestamp", "DarkGreen", "PeachPuff", true)]
        public int Timestamp
        {
            get { return this.timestamp; }
            set { this.timestamp = value; }
        }

        /// <summary>
        /// Gets or sets the links.
        /// </summary>
        /// <value>The links.</value>
        public List<BlobChildLink> Links
        {
            get { return this.links; }
            set { this.links = value; }
        }

        [MarkAttribute("Row Id", "Blue", "PeachPuff", true)]
        public BlobChildLink[] LinksArray
        {
            get { return this.Links.ToArray(); }
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
        [MarkAttribute("Type", "DarkGreen", "PeachPuff", true)]
        public BlobFieldType PointerType
        {
            get { return this.pointerType; }
            set { this.pointerType = value; }
        }
        
        public int Offset
        {
            get { return offset; }
            set { offset = value; }
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
