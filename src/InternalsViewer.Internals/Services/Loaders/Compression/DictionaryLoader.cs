using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Records.CdRecordType;

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

        dictionary.MarkProperty(nameof(Dictionary.EntryCount), offset, sizeof(short));

        dictionary.EntryOffsets = new ushort[entryCount];

        var startEntryOffset = sizeof(short) + sizeof(short) * entryCount;

        var currentOffset = startEntryOffset;

        dictionary.MarkProperty(nameof(Dictionary.EntryOffsets), offset + sizeof(short), entryCount * sizeof(short));

        var entries = new DictionaryEntry[entryCount];

        for (var i = 0; i < entryCount; i++)
        {
            dictionary.EntryOffsets[i] = BitConverter.ToUInt16(data, offset + sizeof(short) + sizeof(short) * i);

            var length = dictionary.EntryOffsets[i] - currentOffset;

            var dictionaryData = new byte[length];

            var entryOffset = offset + currentOffset;

            Array.Copy(data, entryOffset, dictionaryData, 0, length);

            var entry = new DictionaryEntry(i, (ushort)entryOffset, dictionaryData);

            entry.MarkValue(ItemType.DictionaryValue, $"Dictionary Entry {i}", dictionaryData, offset + currentOffset, length);

            entries[i] = entry;

            currentOffset = dictionary.EntryOffsets[i];
        }

        dictionary.DictionaryEntries = entries;

        dictionary.MarkProperty(nameof(Dictionary.DictionaryEntries), offset + startEntryOffset, currentOffset - startEntryOffset); 

        return dictionary;
    }
}