using InternalsViewer.Internals.Compression;

namespace InternalsViewer.Internals.Services.Loaders.Compression;

public static class DictionaryLoader
{
    /// <summary>
    /// Loads a Dictionary Structure
    /// </summary>
    /// <remarks>
    /// Dictionary structure is:
    /// 
    ///     - Entry Count (2 bytes)
    ///     - Entry Offset Array (2 bytes * Entry Count)
    ///     - Dictionary Entries defined by the offset array
    /// </remarks>
    public static Dictionary Load(byte[] data, ushort offset)
    {
        var dictionary = new Dictionary(offset);

        var entryCount = BitConverter.ToInt16(data, offset);

        dictionary.EntryCount = entryCount;

        dictionary.MarkProperty("EntryCount", offset, sizeof(short));

        dictionary.EntryOffset = new ushort[entryCount];

        var dataOffset = sizeof(short) + sizeof(short) * entryCount;

        dictionary.MarkProperty("EntryOffsetArrayDescription", offset + sizeof(short), entryCount * sizeof(short));

        for (var i = 0; i < entryCount; i++)
        {
            dictionary.EntryOffset[i] = BitConverter.ToUInt16(data, offset + sizeof(short) + sizeof(short) * i);

            var length = dictionary.EntryOffset[i] - dataOffset;

            var dictionaryData = new byte[length];

            var entryOffset = offset + dataOffset;

            Array.Copy(data, entryOffset, dictionaryData, 0, length);

            dictionary.MarkProperty("DictionaryEntriesArray", "Dictionary Entry " + i, i);

            var entry = new DictionaryEntry(i, (ushort)entryOffset, dictionaryData);

            entry.MarkProperty("Data", offset + dataOffset, length);

            dictionary.DictionaryEntries.Add(entry);

            dataOffset = dictionary.EntryOffset[i];
        }

        return dictionary;
    }
}