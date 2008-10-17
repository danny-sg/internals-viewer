using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.BlobPointers
{
    public class BlobChildLink
    {
        private int length;
        private int offset;
        private RowIdentifier rowIdentifier;

        public BlobChildLink(RowIdentifier rowIdentifier, int offset, int length)
        {
            this.RowIdentifier = rowIdentifier;
            this.Offset = offset;
            this.Length = length;
        }

        public RowIdentifier RowIdentifier
        {
            get { return this.rowIdentifier; }
            set { this.rowIdentifier = value; }
        }

        // TODO: Can these be changed to Int16?
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
