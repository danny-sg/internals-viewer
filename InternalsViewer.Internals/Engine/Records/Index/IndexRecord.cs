using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.RecordLoaders;
using InternalsViewer.Internals.Records;
using InternalsViewer.Internals.Structures;

namespace InternalsViewer.Internals.Engine.Records.Index
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
        [Mark(MarkType.DownPagePointer)]
        public PageAddress DownPagePointer { get; set; }

        /// <summary>
        /// Gets or sets the RID (Row Identifier) the index is pointing to
        /// </summary>
        /// <value>The rid.</value>
        [Mark(MarkType.Rid)]
        public RowIdentifier Rid { get; set; }

        public IndexTypes IndexType { get; set; }

        public bool IncludeKey { get; set; }
    }
}
