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
        public IndexRecord(Page page, ushort slotOffset, Structure structure)
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
        [Mark("Down Page Pointer", "Navy", "Gainsboro")]
        public PageAddress DownPagePointer { get; set; }

        /// <summary>
        /// Gets or sets the RID (Row Identifier) the index is pointing to
        /// </summary>
        /// <value>The rid.</value>
        [Mark("Down Page Pointer", "Teal", "Gainsboro")]
        public RowIdentifier Rid { get; set; }

        public IndexTypes IndexType { get; set; }

        public bool IncludeKey { get; set; }
    }
}
