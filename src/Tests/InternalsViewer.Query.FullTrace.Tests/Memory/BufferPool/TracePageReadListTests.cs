using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.BufferPool;

[Trait("Category", "Unit")]
public class TracePageReadListTests
{
    [Fact]
    public void Page_Reads_Round_Trip_Through_An_Export()
    {
        var read = new TracePageRead(At(3, 7), 9, 0x20A_5448_05B3, 10);

        using var stream = new MemoryStream();

        new TracePageReadList([read]).Write(new BinaryWriter(stream));

        stream.Position = 0;

        var isSupported = TracePageReadList.IsSupported(new BinaryReader(stream));

        stream.Position = 0;

        var list = TracePageReadList.Read(new BinaryReader(stream));

        Assert.Equal((true, 1, read), (isSupported, list.Count, list[0]));
    }
}
