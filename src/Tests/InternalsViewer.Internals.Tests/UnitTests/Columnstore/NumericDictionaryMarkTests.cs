using System.Buffers.Binary;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Blobs;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Columnstore.Parsers;

namespace InternalsViewer.Internals.Tests.UnitTests.Columnstore;

[Trait("Category", "Unit")]
[Trait("Area", "Columnstore")]
public class NumericDictionaryMarkTests
{
    private static byte[] BuildBlob()
    {
        var data = new byte[NumericDictionary.HeaderSize + 16];

        void Write(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset, 4), value);

        Write(0x00, 1);
        Write(0x04, (int)ColumnstoreLobType.NumericDictionary);
        Write(0x08, 0);

        Write(0x0C, (int)SubLobType.HashTable);
        Write(0x10, 64);
        Write(0x14, 0);
        Write(0x18, 3);
        Write(0x1C, 16);
        Write(0x20, 0);
        Write(0x24, 0);
        Write(0x28, -1);

        Write(0x2C, (int)SubLobType.Array);
        Write(0x30, 8);
        Write(0x34, 2);

        return data;
    }

    [Fact]
    public void Parses_The_Hash_Table_Header()
    {
        var dictionary = (NumericDictionary)DictionaryBlobParser.Parse(BuildBlob(), 2, 4);

        Assert.Equal(SubLobType.HashTable, dictionary.HashTable.SubLobType);
        Assert.Equal(64, dictionary.HashTable.BucketSize);
        Assert.Equal(0, dictionary.HashTable.BucketCount);
        Assert.Equal(3, dictionary.HashTable.MaxLocalEntryCount);
        Assert.Equal(16, dictionary.HashTable.EntrySize);
        Assert.Equal(0, dictionary.HashTable.EntryCount);
        Assert.Equal(0, dictionary.HashTable.CollisionCount);
        Assert.Equal(uint.MaxValue, dictionary.HashTable.BucketIndexMask);

        Assert.False(dictionary.HashTable.IsPopulated);
    }

    [Fact]
    public void Marks_The_Hash_Table_And_Array_Headers_As_Their_Own_Structures()
    {
        var dictionary = (NumericDictionary)DictionaryBlobParser.Parse(BuildBlob(), 2, 4, isMarkEnabled: true);

        var items = dictionary.MarkItems.OfType<PropertyItem>().ToList();

        var hashTable = Assert.Single(items, i => i.PropertyName == nameof(NumericDictionary.HashTable));

        Assert.Equal(NumericDictionaryHashTable.Offset, hashTable.Offset);
        Assert.Equal(NumericDictionaryHashTable.Size, hashTable.Length);

        var valueArray = Assert.Single(items, i => i.PropertyName == nameof(NumericDictionary.ValueArray));

        Assert.Equal(NumericDictionaryValueArray.Offset, valueArray.Offset);
        Assert.Equal(NumericDictionaryValueArray.Size, valueArray.Length);

        Assert.Equal(8, dictionary.HashTable.MarkItems.Count);
        Assert.Equal(3, dictionary.ValueArray.MarkItems.Count);
    }

    [Fact]
    public void Marks_Hash_Table_Fields_Against_The_Blob_Not_The_Sub_Structure()
    {
        var dictionary = (NumericDictionary)DictionaryBlobParser.Parse(BuildBlob(), 2, 4, isMarkEnabled: true);

        var items = dictionary.HashTable.MarkItems.OfType<PropertyItem>().ToList();

        Assert.Equal(0x0C, Assert.Single(items, i => i.PropertyName == nameof(NumericDictionaryHashTable.SubLobType)).Offset);
        Assert.Equal(0x28, Assert.Single(items, i => i.PropertyName == nameof(NumericDictionaryHashTable.BucketIndexMask)).Offset);

        Assert.All(items, i => Assert.Equal(4, i.Length));
    }

    [Fact]
    public void Leaves_The_Headers_Unmarked_When_Marking_Is_Off()
    {
        var dictionary = (NumericDictionary)DictionaryBlobParser.Parse(BuildBlob(), 2, 4);

        Assert.Empty(dictionary.MarkItems);
        Assert.Empty(dictionary.HashTable.MarkItems);
        Assert.Empty(dictionary.ValueArray.MarkItems);
    }
}
