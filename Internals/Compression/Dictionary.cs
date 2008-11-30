using System;
using System.Collections.Generic;
using System.Text;

namespace InternalsViewer.Internals.Compression
{
    public class Dictionary
    {
        private short[] entryOffset;
        private int entryCount;
        private int offset;

        private List<byte[]> entries = new List<byte[]>();

        public Dictionary(byte[] data, int offset)
        {
            this.Offset = offset;
            this.LoadDictionary(data, offset);
        }

        private void LoadDictionary(byte[] data, int offset)
        {
            this.EntryCount = BitConverter.ToInt16(data, offset);

            this.EntryOffset = new short[this.EntryCount];

            int dataOffset = sizeof(Int16) + (sizeof(Int16) * this.EntryCount);

            for (int i = 0; i < this.EntryCount; i++)
            {
                this.entryOffset[i] = BitConverter.ToInt16(data, offset + sizeof(Int16) + (sizeof(Int16) * i));

                int length = this.entryOffset[i] - dataOffset;

                byte[] dictionaryData = new byte[length];

                Array.Copy(data, offset + dataOffset, dictionaryData, 0, length);

                this.entries.Add(dictionaryData);

                dataOffset = this.entryOffset[i];
            }
        }

        public int Offset
        {
            get { return this.offset; }
            set { this.offset = value; }
        }

        public List<byte[]> DictionaryEntries
        {
            get { return this.entries; }
            set { this.entries = value; }
        }

        public int EntryCount
        {
            get { return this.entryCount; }
            set { this.entryCount = value; }
        }

        public short[] EntryOffset
        {
            get { return this.entryOffset; }
            set { this.entryOffset = value; }
        }
    }
}
