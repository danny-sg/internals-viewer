using System;
using System.Text;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.RecordLoaders;
using InternalsViewer.Internals.Structures;

namespace InternalsViewer.Internals.Records
{
    public class DataRecord : Record
    {
        private SparseVector sparseVector;

        public DataRecord(Page page, UInt16 slotOffset, Structure structure)
            : base(page, slotOffset, structure)
        {
            DataRecordLoader.Load(this);
        }

        internal static string GetStatusBitsDescription(DataRecord dataRecord)
        {
            string statusDescription = string.Empty;

            if (dataRecord.HasVariableLengthColumns)
            {
                statusDescription += ", Variable Length Flag";
            }

            if (dataRecord.HasNullBitmap && dataRecord.HasVariableLengthColumns)
            {
                statusDescription += " | NULL Bitmap Flag";
            }
            else if (dataRecord.HasNullBitmap)
            {
                statusDescription += ", NULL Bitmap Flag";
            }

            return statusDescription;
        }

        public SparseVector SparseVector
        {
            get { return sparseVector; }
            set { sparseVector = value; }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendFormat("DataRecord | Page: {0} | Slot Offset: {1} | Allocation Unit: {2}\n",
                            this.Page.Header.PageAddress,
                            this.SlotOffset,
                            this.Page.Header.AllocationUnit);

            sb.Append("-----------------------------------------------------------------------------------------\n");
            sb.AppendFormat("Status Bits A:                {0}\n", GetStatusBitsDescription(this));
            sb.AppendFormat("Column count offset:          {0}\n", this.ColumnCountOffset);
            sb.AppendFormat("Number of columns:            {0}\n", this.ColumnCount);
            sb.AppendFormat("Null bitmap:                  {0}\n", this.HasNullBitmap ? GetNullBitmapString(this.NullBitmap) : "(No null bitmap)");
            sb.AppendFormat("Variable length column count: {0}\n", this.VariableLengthColumnCount);
            sb.AppendFormat("Column offset array:          {0}\n", this.HasVariableLengthColumns ? GetArrayString(this.ColOffsetArray) : "(no variable length columns)");

            foreach (RecordField field in this.Fields)
            {
                sb.AppendLine(field.ToString());
            }
            return sb.ToString();
        }

        [MarkAttribute("Status Bits A", "Red", "Gainsboro", true)]
        public string StatusBitsADescription
        {
            get { return GetRecordTypeDescription(this.RecordType) + GetStatusBitsDescription(this); }
        }

        [MarkAttribute("Status Bits B", "Maroon", "Gainsboro", true)]
        public string StatusBitsBDescription
        {
            get { return ""; }
        }

    }
}
