using System;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.Compression
{
    public class Dictionary : Markable
    {
        private ushort[] entryOffset;
        private int entryCount;
        private int offset;

        private List<DictionaryEntry> entries = new List<DictionaryEntry>();

        public Dictionary(byte[] data, int offset)
        {
            this.Offset = offset;
            this.LoadDictionary(data, offset);
        }

        private void LoadDictionary(byte[] data, int offset)
        {
            this.EntryCount = BitConverter.ToInt16(data, offset);

            this.Mark("EntryCount", offset, sizeof(Int16));

            this.EntryOffset = new ushort[this.EntryCount];

            int dataOffset = sizeof(Int16) + (sizeof(Int16) * this.EntryCount);

            this.Mark("EntryOffsetArrayDescription", offset + sizeof(Int16), this.EntryCount * sizeof(Int16));

            for (int i = 0; i < this.EntryCount; i++)
            {
                this.entryOffset[i] = BitConverter.ToUInt16(data, offset + sizeof(Int16) + (sizeof(Int16) * i));

                int length = this.entryOffset[i] - dataOffset;

                byte[] dictionaryData = new byte[length];

                Array.Copy(data, offset + dataOffset, dictionaryData, 0, length);

                this.Mark("DictionaryEntriesArray", "Dictionary Entry " + i, i);

                DictionaryEntry entry = new DictionaryEntry(dictionaryData);

                entry.Mark("Data", offset + dataOffset, length);

                this.entries.Add(entry);

                dataOffset = this.entryOffset[i];
            }
        }

        public int Offset
        {
            get { return this.offset; }
            set { this.offset = value; }
        }

        public List<DictionaryEntry> DictionaryEntries
        {
            get { return this.entries; }
            set { this.entries = value; }
        }

        public DictionaryEntry[] DictionaryEntriesArray
        {
            get { return this.entries.ToArray(); }
        }

        [MarkAttribute("Entry Count", "AliceBlue", "Gainsboro", true)]
        public int EntryCount
        {
            get { return this.entryCount; }
            set { this.entryCount = value; }
        }

        public ushort[] EntryOffset
        {
            get { return this.entryOffset; }
            set { this.entryOffset = value; }
        }

        [MarkAttribute("Column Offset Array", "Blue", "AliceBlue", true)]
        public string EntryOffsetArrayDescription
        {
            get { return Record.GetArrayString(this.entryOffset); }
        }
    }
}
