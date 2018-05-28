using InternalsViewer.Internals.Pages;
using System.Collections.Generic;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.BlobPointers
{
    public class BlobChildLink: Markable
    {
        private List<MarkItem> markItems = new List<MarkItem>();

        public BlobChildLink()
        {
        }

        public BlobChildLink(RowIdentifier rowIdentifier, int offset, int length)
        {
            RowIdentifier = rowIdentifier;
            Offset = offset;
            Length = length;
        }

        [MarkAttribute("Row Identifier", "DarkMagenta", "Thistle", true)]
        public RowIdentifier RowIdentifier { get; set; }

        [MarkAttribute("Offset", "Blue", "Thistle", true)]
        public int Offset { get; set; }

        [MarkAttribute("Length", "Red", "Thistle", true)]
        public int Length { get; set; }

        public override string ToString()
        {
            return string.Format("Page: {0} Offset: {1} Length: {2}", RowIdentifier, Offset, Length);
        }
    }
}
