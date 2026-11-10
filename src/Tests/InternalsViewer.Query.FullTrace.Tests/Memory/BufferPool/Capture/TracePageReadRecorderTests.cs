using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.BufferPool.Capture;
using InternalsViewer.Query.FullTrace.Native;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.BufferPool.Capture;

[Trait("Category", "Unit")]
public class TracePageReadRecorderTests
{
    private const ulong Frame = 0x20A_5448_0000;

    private const ulong OtherFrame = 0x20A_5448_2000;

    private const ushort Database = 6;

    private const uint Thread = 9;

    private const uint OtherThread = 10;

    [Fact]
    public void Each_Page_Frame_Seen_In_The_Replay_Is_Watched()
        => Assert.Equal([new WatchRange(Frame, 0x2000), new WatchRange(OtherFrame, 0x2000)], Recorder().GetRanges().OrderBy(r => r.Address));

    [Fact]
    public void Touching_Reads_Of_A_Row_Are_One_Page_Read()
    {
        var reads = Record(Read(3, Frame + 0x5B3, 4), Read(4, Frame + 0x5B7, 4), Read(5, Frame + 0x5BB, 2));

        Assert.Equal([new TracePageRead(At(3), Thread, Frame + 0x5B3, 10)], reads);
    }

    [Fact]
    public void A_Slot_Array_Read_And_The_Record_It_Points_At_Are_Separate_Page_Reads()
        => Assert.Equal([Frame + 0x1FF6, Frame + 0x5B3],
                        Record(Read(3, Frame + 0x1FF6, 2), Read(4, Frame + 0x5B3, 4)).Select(r => r.Address));

    [Fact]
    public void A_Read_Of_The_Page_Header_Is_A_Page_Read()
        => Assert.Equal([new TracePageRead(At(3), Thread, Frame + 0x20, 2)], Record(Read(3, Frame + 0x20, 2)));

    [Fact]
    public void A_Read_Crossing_From_The_Header_Into_The_Body_Is_A_Page_Read_Of_Each()
        => Assert.Equal([new TracePageRead(At(3), Thread, Frame + 0x40, 0x20), new TracePageRead(At(3), Thread, Frame + 0x60, 0x1FA0)],
                        Record(Read(3, Frame + 0x40, 0x40), Read(4, Frame + 0x80, 0x1F80)));

    [Fact]
    public void A_Header_Read_Does_Not_Split_Touching_Reads_Of_A_Row()
        => Assert.Equal([new TracePageRead(At(3), Thread, Frame + 0x5B3, 8), new TracePageRead(At(4), Thread, Frame + 0x16, 2)],
                        Record(Read(3, Frame + 0x5B3, 4), Read(4, Frame + 0x16, 2), Read(5, Frame + 0x5B7, 4)));

    [Fact]
    public void Touching_Reads_Across_Two_Frames_Are_Separate_Page_Reads()
        => Assert.Equal(2, Record(Read(3, OtherFrame - 4, 4), Read(4, OtherFrame + 0x60, 4)).Count);

    [Fact]
    public void Reads_On_Different_Threads_Are_Kept_Apart()
        => Assert.Equal(2, Record(Read(3, Frame + 0x5B3, 4), Read(4, Frame + 0x5B7, 4, OtherThread)).Count);

    private static TracePageReadRecorder Recorder()
    {
        var builder = new TraceBufferPool.Builder();

        builder.Add(new TraceBufferPage(Frame, new PageAddress(1, 405), Database, At(1)));

        builder.Add(new TraceBufferPage(OtherFrame, new PageAddress(1, 406), Database, At(2)));

        return new TracePageReadRecorder(builder.Build());
    }

    private static List<TracePageRead> Record(params WatchHit[] hits)
    {
        var recorder = Recorder();

        foreach (var hit in hits)
        {
            recorder.Add(hit);
        }

        var reads = recorder.Build();

        return [.. Enumerable.Range(0, reads.Count).Select(i => reads[i])];
    }

    private static WatchHit Read(ulong sequence, ulong address, uint size, uint thread = Thread)
        => new(sequence, 0, address, 0, thread, size, 0, 0);
}
