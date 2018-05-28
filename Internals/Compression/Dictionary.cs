using System;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.Compression
{
    public class Dictionary : Markable
    {
        public Dictionary(byte[] data, int offset)
        {
            Offset = offset;
            LoadDictionary(data, offset);
        }

        private void LoadDictionary(byte[] data, int offset)
        {
            EntryCount = BitConverter.ToInt16(data, offset);

            Mark("EntryCount", offset, sizeof(Int16));

            EntryOffset = new ushort[EntryCount];

            var dataOffset = sizeof(Int16) + (sizeof(Int16) * EntryCount);

            Mark("EntryOffsetArrayDescription", offset + sizeof(Int16), EntryCount * sizeof(Int16));

            for (var i = 0; i < EntryCount; i++)
            {
                EntryOffset[i] = BitConverter.ToUInt16(data, offset + sizeof(Int16) + (sizeof(Int16) * i));

                var length = EntryOffset[i] - dataOffset;

                var dictionaryData = new byte[length];

                Array.Copy(data, offset + dataOffset, dictionaryData, 0, length);

                Mark("DictionaryEntriesArray", "Dictionary Entry " + i, i);

                var entry = new DictionaryEntry(dictionaryData);

                entry.Mark("Data", offset + dataOffset, length);

                DictionaryEntries.Add(entry);

                dataOffset = EntryOffset[i];
            }
        }

        public int Offset { get; set; }

        public List<DictionaryEntry> DictionaryEntries { get; set; } = new List<DictionaryEntry>();

        public DictionaryEntry[] DictionaryEntriesArray
        {
            get { return DictionaryEntries.ToArray(); }
        }

        [MarkAttribute("Entry Count", "AliceBlue", "Gainsboro", true)]
        public int EntryCount { get; set; }

        public ushort[] EntryOffset { get; set; }

        [MarkAttribute("Column Offset Array", "Blue", "AliceBlue", true)]
        public string EntryOffsetArrayDescription
        {
            get { return Record.GetArrayString(EntryOffset); }
        }
    }
}
