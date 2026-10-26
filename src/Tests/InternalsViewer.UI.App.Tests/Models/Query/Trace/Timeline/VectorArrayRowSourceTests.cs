using InternalsViewer.Query.FullTrace.Memory.History.Batches;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;

namespace InternalsViewer.UI.App.Tests.Models.Query.Trace.Timeline;

[Trait("Category", "Unit")]
public class VectorArrayRowSourceTests
{
    private const ulong Block = 0x2000;

    [Fact]
    public void Each_Vector_Shows_Its_Entry_Offset_And_A_Link_To_Its_Buffer()
    {
        var opened = new List<ulong>();

        BatchVector[] vectors = [new(1, 0x5040, false, [], []), new(2, 0, false, [], [])];

        var source = new VectorArrayRowSource(new BatchState(0x1000, 900, 0, Block, 0, 0, 0, [], 0, [.. vectors]), Block, opened.Add);

        var link = Assert.IsType<ActionLinks>(source.Value(0, 2)).Links.Single();

        link.Invoke();

        Assert.Equal(("0x0", "0x58", "0x0000000000005040", null), (source.Value(0, 1), source.Value(1, 1), link.Text, source.Value(1, 2)));
        Assert.Equal([0x5040UL], opened);
    }
}
