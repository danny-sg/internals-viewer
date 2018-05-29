using System;
using System.Text;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.RecordLoaders;
using InternalsViewer.Internals.Structures;

namespace InternalsViewer.Internals.Records
{
    public class DataRecord : Record
    {
        public DataRecord(Page page, ushort slotOffset, Structure structure)
            : base(page, slotOffset, structure)
        {
            DataRecordLoader.Load(this);
        }

        public SparseVector SparseVector { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendFormat("DataRecord | Page: {0} | Slot Offset: {1} | Allocation Unit: {2}\n",
                            Page.Header.PageAddress,
                            SlotOffset,
                            Page.Header.AllocationUnit);

            sb.Append("-----------------------------------------------------------------------------------------\n");
            sb.AppendFormat("Status Bits A:                {0}\n", GetStatusBitsDescription(this));
            sb.AppendFormat("Column count offset:          {0}\n", ColumnCountOffset);
            sb.AppendFormat("Number of columns:            {0}\n", ColumnCount);
            sb.AppendFormat("Null bitmap:                  {0}\n", HasNullBitmap ? GetNullBitmapString(NullBitmap) : "(No null bitmap)");
            sb.AppendFormat("Variable length column count: {0}\n", VariableLengthColumnCount);
            sb.AppendFormat("Column offset array:          {0}\n", HasVariableLengthColumns ? GetArrayString(ColOffsetArray) : "(no variable length columns)");

            foreach (var field in Fields)
            {
                sb.AppendLine(field.ToString());
            }
            return sb.ToString();
        }

        [Mark("Status Bits B", "Maroon", "Gainsboro")]
        public string StatusBitsBDescription => "";

        [Mark("Forwarding Record", "DarkBlue", "Gainsboro")]
        public RowIdentifier ForwardingRecord { get; set; }
    }
}
