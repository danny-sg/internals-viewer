using InternalsViewer.Query.Events.BatchMode.Enums;
using InternalsViewer.Query.Memory;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class ObjectPoolReaderTests
{
    [Fact]
    public void Parse_Reads_The_Object_From_The_Entry_Data()
    {
        const string entryData = "<object_type='2' db_id='6' hobt_id ='72057594048020480' "
                                 + "column_id='6' object_id ='0' object_size='18796'/>";

        var entry = ObjectPoolReader.Parse(0x100, 0x200, 0x300, 24, entryData);

        Assert.Equal(new ObjectPoolEntry(0x100,
                                         0x200,
                                         0x300,
                                         24,
                                         ColumnStoreObjectType.PrimaryDictionary,
                                         6,
                                         72057594048020480,
                                         6,
                                         0,
                                         18796),
                     entry);
    }

    [Fact]
    public void Parse_Keeps_A_Delete_Bitmap_Without_A_Column()
    {
        const string entryData = "<object_type='6' db_id='6' hobt_id ='72057594048020480' "
                                 + "column_id='-1' object_id ='3' object_size='4'/>";

        var entry = ObjectPoolReader.Parse(0x100, 0x200, 0x300, 8, entryData);

        Assert.NotNull(entry);
        Assert.Equal(ColumnStoreObjectType.DeleteBitmap, entry.ObjectType);
        Assert.Equal(-1, entry.ColumnId);
        Assert.Equal(3, entry.ObjectId);
    }

    [Theory]
    [InlineData("<db_id='6' hobt_id ='72057594048020480' column_id='6'/>")]
    [InlineData("<object_type='1' db_id='6' column_id='6'/>")]
    [InlineData("")]
    public void Parse_Skips_An_Entry_Without_Its_Object(string entryData)
    {
        Assert.Null(ObjectPoolReader.Parse(0x100, 0x200, 0x300, 8, entryData));
    }
}
