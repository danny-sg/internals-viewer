using InternalsViewer.Internals.Pages;
using System.Collections.Generic;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.BlobPointers
{
    public class BlobChildLink: Markable
    {
        private int length;
        private int offset;
        private RowIdentifier rowIdentifier;
        private List<MarkItem> markItems = new List<MarkItem>();

        public BlobChildLink()
        {
        }

        public BlobChildLink(RowIdentifier rowIdentifier, int offset, int length)
        {
            this.RowIdentifier = rowIdentifier;
            this.Offset = offset;
            this.Length = length;
        }

        [MarkAttribute("Row Identifier", "DarkMagenta", "Thistle", true)]
        public RowIdentifier RowIdentifier
        {
            get { return this.rowIdentifier; }
            set { this.rowIdentifier = value; }
        }

        [MarkAttribute("Offset", "Blue", "Thistle", true)]
        public int Offset
        {
            get { return this.offset; }
            set { this.offset = value; }
        }

        public int Length
        {
            get { return this.length; }
            set { this.length = value; }
        }

        public override string ToString()
        {
            return string.Format("Page: {0} Offset: {1} Length: {2}", this.RowIdentifier, this.Offset, this.Length);
        }
    }
}
