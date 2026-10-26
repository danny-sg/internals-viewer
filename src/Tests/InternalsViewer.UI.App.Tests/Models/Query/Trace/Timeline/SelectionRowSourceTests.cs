using InternalsViewer.Query.FullTrace.Memory.History.Batches;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;

namespace InternalsViewer.UI.App.Tests.Models.Query.Trace.Timeline;

[Trait("Category", "Unit")]
public class SelectionRowSourceTests
{
    private const ulong Block = 0x3000;

    private static readonly BatchState State = new(0x1000, 900, 3, 0x2000, 0, 0, Block, [0, 1, 0x102], 0x103, []);

    [Fact]
    public void Each_Entry_Shows_Its_Offset_Row_And_Raw_Little_Endian_Bytes()
    {
        var source = new SelectionRowSource(State, Block);

        Assert.Equal([(object?)"0x0", 0, "00 00"], Enumerable.Range(1, 3).Select(o => source.Value(0, o)));
        Assert.Equal([(object?)"0x4", 0x102, "02 01"], Enumerable.Range(1, 3).Select(o => source.Value(2, o)));
    }
}
