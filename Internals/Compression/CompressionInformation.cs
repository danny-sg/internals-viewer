using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.Records;
using InternalsViewer.Internals.Structures;

namespace InternalsViewer.Internals.Compression
{
    public class CompressionInformation: Markable
    {
        public static byte CiSize = 7;
        public static short Offset = 96;

        public CompressionInformation(Page page, int slot)
        {
            Page = page;
            SlotOffset = slot;

            try
            {
                LoadCompressionInfo(this);
            }
            catch
            {
                Debug.Print("Load Failed");
            }
        }

        private static void LoadCompressionInfo(CompressionInformation ci)
        {
            ci.StatusBits = new BitArray(new byte[] { ci.Page.PageData[ci.SlotOffset] });

            ci.Mark("StatusDescription", ci.SlotOffset, 1);

            ci.HasAnchorRecord = ci.StatusBits[1];
            ci.HasDictionary = ci.StatusBits[2];

            ci.PageModCount = BitConverter.ToInt16(ci.Page.PageData, ci.SlotOffset + 1);
            
            ci.Mark("PageModCount", ci.SlotOffset + sizeof(byte), sizeof(Int16));

            ci.Length = BitConverter.ToInt16(ci.Page.PageData, ci.SlotOffset + 3);

            ci.Mark("Length", ci.SlotOffset + sizeof(byte) + sizeof(Int16), sizeof(Int16));

            if (ci.HasDictionary)
            {
                ci.Size = BitConverter.ToInt16(ci.Page.PageData, ci.SlotOffset + 5);

                ci.Mark("Size", ci.SlotOffset + sizeof(byte) + sizeof(Int16) + sizeof(Int16), sizeof(Int16));
            }

            if (ci.HasAnchorRecord)
            {
                CompressionInformation.LoadAnchor(ci.HasDictionary, ci);
            }
            if (ci.HasDictionary)
            {
                CompressionInformation.LoadDictionary(ci);
            }
        }

        private static void LoadDictionary(CompressionInformation ci)
        {
            ci.CompressionDictionary = new Dictionary(ci.Page.PageData, CompressionInformation.Offset + ci.Length);
        }

        private static void LoadAnchor(bool hasDictionary, CompressionInformation ci)
        {
            var startOffset = (ci.HasDictionary ? 7 : 5) + ci.SlotOffset;

            int records = ci.Page.PageData[startOffset + 1];

            var structure = CreateTableStructure(records, ci);

            ci.AnchorRecord = new CompressedDataRecord(ci.Page, (UInt16)startOffset, structure);
        }

        private static TableStructure CreateTableStructure(int records, CompressionInformation ci)
        {
            var structure = new TableStructure(ci.Page.Header.AllocationUnitId, ci.Page.Database);

            var fields = new List<RecordField>();

            for (short i = 0; i < records; i++)
            {
                var column = new Column();

                column.ColumnName = string.Format("Column {0}", i);
                column.ColumnId = i;
                column.LeafOffset = i;
                column.DataType = SqlDbType.VarBinary;
                column.DataLength = 8000;

                structure.Columns.Add(column);
            }

            return structure;
        }

        [MarkAttribute("Page Mod Count", "DarkGreen", "Gainsboro", true)]
        public short PageModCount { get; set; }

        [MarkAttribute("Size", "Purple", "Gainsboro", true)]
        public short Size { get; set; }

        public BitArray StatusBits { get; set; }

        public int SlotOffset { get; set; }

        public Page Page { get; set; }

        [MarkAttribute("Status Bits A", "Red", "Gainsboro", true)]
        public string StatusDescription
        {
            get
            {
                var sb = new StringBuilder();

                if (HasAnchorRecord)
                {
                    sb.Append("Has Anchor Record");
                }

                if (HasAnchorRecord && HasDictionary)
                {
                    sb.Append(", ");
                }

                if (HasDictionary)
                {
                    sb.Append("Has Dictionary");
                }

                return sb.ToString();
            }
        }
        public CompressedDataRecord AnchorRecord { get; set; }

        public Dictionary CompressionDictionary { get; set; }

        public bool HasAnchorRecord { get; set; }

        public bool HasDictionary { get; set; }

        [MarkAttribute("Length", "Blue", "Gainsboro", true)]
        public short Length { get; set; }

        public enum CompressionInfoStructure
        {
            None,
            Header,
            Anchor,
            Dictionary
        }
    }
}
