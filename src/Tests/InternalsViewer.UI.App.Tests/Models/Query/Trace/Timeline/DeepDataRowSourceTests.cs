using System.Data;
using System.Text;
using InternalsViewer.Query.FullTrace.Memory.History.Batches;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;

namespace InternalsViewer.UI.App.Tests.Models.Query.Trace.Timeline;

[Trait("Category", "Unit")]
public class DeepDataRowSourceTests
{
    private const ulong Page = 0x10000;

    private static readonly List<DeepDataEntry> Entries = [new(Page + 0x18, 6, Encoding.Latin1.GetBytes("Madrid"))];

    [Fact]
    public void Each_Entry_Shows_Its_Offset_And_Length()
    {
        var source = new DeepDataRowSource(Entries, Page, () => null);

        Assert.Equal([(object?)0, "0x18", 6], Enumerable.Range(0, 3).Select(o => source.Value(0, o)));
    }

    [Theory]
    [InlineData(null, "0x4D6164726964")]
    [InlineData(SqlDbType.VarChar, "Madrid")]
    public void The_Value_Shows_As_Text_Only_For_A_Character_Type(SqlDbType? type, string expected)
        => Assert.Equal(expected, new DeepDataRowSource(Entries, Page, () => type).Value(0, 3));
}
