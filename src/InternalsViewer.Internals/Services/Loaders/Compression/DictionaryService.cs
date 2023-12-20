using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;

namespace InternalsViewer.Internals.Services.Loaders.Compression;

public class DictionaryService : IDictionaryService
{
    public Dictionary Load(byte[] data, int offset)
    {
        var dictionary = new Dictionary(offset);

        var entryCount = BitConverter.ToInt16(data, offset);

        dictionary.EntryCount = entryCount;

        dictionary.MarkDataStructure("EntryCount", offset, sizeof(short));

        dictionary.EntryOffset = new ushort[entryCount];

        var dataOffset = sizeof(short) + sizeof(short) * entryCount;

        dictionary.MarkDataStructure("EntryOffsetArrayDescription", offset + sizeof(short), entryCount * sizeof(short));

        for (var i = 0; i < entryCount; i++)
        {
            dictionary.EntryOffset[i] = BitConverter.ToUInt16(data, offset + sizeof(short) + sizeof(short) * i);

            var length = dictionary.EntryOffset[i] - dataOffset;

            var dictionaryData = new byte[length];

            Array.Copy(data, offset + dataOffset, dictionaryData, 0, length);

            dictionary.MarkDataStructure("DictionaryEntriesArray", "Dictionary Entry " + i, i);

            var entry = new DictionaryEntry(dictionaryData);

            entry.MarkDataStructure("Data", offset + dataOffset, length);

            dictionary.DictionaryEntries.Add(entry);

            dataOffset = dictionary.EntryOffset[i];
        }

        return dictionary;
    }
}