using System.Data;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;

namespace InternalsViewer.UI.App.Tests.Models.Query.Trace.Timeline;

[Trait("Category", "Unit")]
public class BatchSlotFormatterTests
{
    [Theory]
    [InlineData(0x600220UL, SqlDbType.Int, "3146000")]
    [InlineData(0x168720UL, SqlDbType.Date, "2022-02-07")]
    [InlineData(0x3BB6F9BFB3AF7B76UL, SqlDbType.Float, "1E-10")]
    [InlineData(0xE60100UL, SqlDbType.VarChar, "Dictionary 128 Entry 230")]
    [InlineData(0x21C1D33405DUL, SqlDbType.VarChar, "Deep Data 0x21C1D33405C")]
    [InlineData(1UL, SqlDbType.Int, "NULL")]
    public void A_Slot_Decodes_As_The_Chosen_Type(ulong slot, SqlDbType type, string expected)
        => Assert.Equal(expected, BatchSlotFormatter.Format(slot, type));

    [Fact]
    public void No_Type_Shows_The_Raw_Slot()
        => Assert.Equal("0x0000000000600220", BatchSlotFormatter.Format(0x600220, null));
}
