using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.BufferPool;

[Trait("Category", "Unit")]
public class TraceBufferPoolTests
{
    private const ulong Frame = 0x20A_5448_0000;

    private const ulong OtherFrame = 0x20A_5448_2000;

    private const ushort Database = 6;

    private const ulong Buffer = 0x14A_BB17_8340;

    private static readonly PageAddress First = new(1, 100);

    private static readonly PageAddress Second = new(1, 200);

    [Fact]
    public void A_Pointer_Into_A_Frame_Resolves_To_Its_Page_And_Offset()
        => Assert.Equal(new TraceBufferReference(First, Database, 0x1A0), Build((Frame, First, 2)).GetReference(Frame + 0x1A0, At(5)));

    [Fact]
    public void A_Reused_Frame_Resolves_To_The_Page_It_Held_At_The_Position()
    {
        var pool = Build((Frame, First, 2), (Frame, Second, 8));

        Assert.Equal((First, Second), (pool.GetReference(Frame, At(5))?.Page, pool.GetReference(Frame, At(9))?.Page));
    }

    [Fact]
    public void A_Pointer_Before_Its_Frame_Was_Fetched_Resolves_To_Nothing()
        => Assert.Null(Build((Frame, First, 2)).GetReference(Frame + 8, At(1)));

    [Fact]
    public void A_Pointer_Outside_Every_Fetched_Frame_Resolves_To_Nothing()
        => Assert.Null(Build((Frame, First, 2)).GetReference(OtherFrame + 8, At(5)));

    [Fact]
    public void Repeated_Fetches_Of_The_Same_Page_Are_Stored_Once()
        => Assert.Equal(2, Build((Frame, First, 4), (OtherFrame, Second, 3), (Frame, First, 2)).Count);

    [Fact]
    public void Pages_Round_Trip_Through_An_Export()
    {
        using var stream = new MemoryStream();

        Build((Frame, First, 2), (Frame, Second, 8)).Write(new BinaryWriter(stream));

        stream.Position = 0;

        var read = TraceBufferPool.Read(new BinaryReader(stream));

        Assert.Equal((2, new TraceBufferReference(Second, Database, 4)), (read.Count, read.GetReference(Frame + 4, At(9))));
    }

    [Fact]
    public void A_Pointer_Into_A_Buffer_Resolves_To_The_Page_It_Described_At_The_Position()
    {
        var pool = Build((Frame, First, 2, Buffer), (OtherFrame, Second, 8, Buffer));

        Assert.Equal((new TraceBufferReference(First, Database, 0x98, IsBuffer: true), Second),
                     (pool.GetReference(Buffer + 0x98, At(5)), pool.GetReference(Buffer, At(9))?.Page));
    }

    [Fact]
    public void Only_The_Start_Of_A_Fetched_Buffer_Is_A_Buffer()
    {
        var pool = Build((Frame, First, 2, Buffer));

        Assert.Equal([true, false, false], [pool.IsBuffer(Buffer), pool.IsBuffer(Buffer + 0x98), pool.IsBuffer(Frame)]);
    }

    [Fact]
    public void A_Pointer_Past_The_End_Of_A_Buffer_Resolves_To_Nothing()
        => Assert.Null(Build((Frame, First, 2, Buffer)).GetReference(Buffer + 0xC0, At(5)));

    [Fact]
    public void A_Page_Read_Before_Its_Fetch_Keeps_The_Fetch_Buffer()
        => Assert.Equal(new TraceBufferReference(First, Database, 0, IsBuffer: true),
                        Build((Frame, First, 2, 0), (Frame, First, 4, Buffer)).GetReference(Buffer, At(3)));

    [Fact]
    public void A_First_Format_Export_Reads_Without_Buffers()
    {
        using var stream = new MemoryStream();

        var writer = new BinaryWriter(stream);

        writer.Write(-1);
        writer.Write(1);
        writer.Write(Frame);
        writer.Write(First.FileId);
        writer.Write(First.PageId);
        writer.Write(Database);
        writer.Write(2ul);
        writer.Write(0ul);

        stream.Position = 0;

        var read = TraceBufferPool.Read(new BinaryReader(stream));

        Assert.Equal((First, (TraceBufferReference?)null), (read.GetReference(Frame, At(3))?.Page, read.GetReference(Buffer, At(3))));
    }

    private static TraceBufferPool Build(params (ulong Frame, PageAddress Page, ulong Sequence)[] pages)
        => Build(pages.Select(p => (p.Frame, p.Page, p.Sequence, 0ul)).ToArray());

    private static TraceBufferPool Build(params (ulong Frame, PageAddress Page, ulong Sequence, ulong Buffer)[] pages)
    {
        var builder = new TraceBufferPool.Builder();

        foreach (var (frame, page, sequence, buffer) in pages)
        {
            builder.Add(new TraceBufferPage(frame, page, Database, At(sequence), buffer));
        }

        return builder.Build();
    }
}
