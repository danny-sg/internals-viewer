using System.Data;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers.Records;
using InternalsViewer.UI.App.Models.Query.CallStack;

namespace InternalsViewer.UI.App.Tests.Models.Query.CallStack;

[Trait("Category", "Unit")]
public class RowBufferDisplayTests
{
    private static readonly RowRecordLayout Layout = new([Column("Id", 4), Column("RowSpacer", 800), Column("Id", 4)], keyCount: 1);

    private static readonly RowBufferDecoder Decoder = new(null!, _ => null);

    [Fact]
    public void A_Hash_Table_Column_Is_Labelled_By_Its_Name()
        => Assert.Equal("Id", RowBufferDisplay.GetLabel(Row(), Field(0), Decoder));

    [Fact]
    public void A_Build_Key_Stored_After_The_Columns_Is_Labelled_As_The_Key()
        => Assert.Equal("Id (Build Key)", RowBufferDisplay.GetLabel(Row(), Field(2), Decoder));

    private static RowBuffer Row()
        => new(0, 0, 0, new TracePosition(1, 0), 0, 0, [Field(0), Field(1), Field(2)], RowBufferFormat.HashTable)
        {
            RecordLayout = Layout
        };

    private static RowBufferColumn Field(int ordinal) => new(ordinal, RowBufferBinding.None, 0, RowBufferColumnFlags.None, 0, []);

    private static ColumnStructure Column(string name, short length)
        => new() { ColumnName = name, DataType = SqlDbType.Int, DataLength = length };
}
