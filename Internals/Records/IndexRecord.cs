using System;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Internals.RecordLoaders;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.Structures;

namespace InternalsViewer.Internals.Records
{
    public class IndexRecord : Record
    {
        public IndexRecord(Page page, UInt16 slotOffset, Structure structure)
            : base(page, slotOffset, structure)
        {
            IndexRecordLoader.Load(this);
        }

        public bool IsIndexType(IndexTypes index)
        {
            return (IndexType & index) == index;
        }

        /// <summary>
        /// Gets or sets down page pointer to the next page in the index
        /// </summary>
        /// <value>Down page pointer.</value>
        [MarkAttribute("Down Page Pointer", "Navy", "Gainsboro", true)]
        public PageAddress DownPagePointer { get; set; }

        /// <summary>
        /// Gets or sets the RID (Row Identifier) the index is pointing to
        /// </summary>
        /// <value>The rid.</value>
        [MarkAttribute("Down Page Pointer", "Teal", "Gainsboro", true)]
        public RowIdentifier Rid { get; set; }

        public IndexTypes IndexType { get; set; }

        public bool IncludeKey { get; set; }
    }
}
