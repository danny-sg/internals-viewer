using System.Text;
using InternalsViewer.Query.FullTrace.Batches;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Batches.Enums;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class BatchHookTests
{
    private const ulong Function = 0x7FF8_0050_0000;

    private const ulong Iterator = 0x21C_0BB4_80F0;

    private const ulong Consumer = 0x21C_0BB4_C180;

    private const ulong Data = 0x55_47EF_B230;

    private const ulong Batch = 0x21C_0BB3_CCA0;

    private const ulong Ids = 0x55_47EF_B300;

    private const ulong Vectors = 0x21C_0BB4_0000;

    private const ulong Pure = 0x21C_0BB4_1000;

    private const ulong Selection = 0x21C_0BB4_2000;

    private const ulong FirstBuffer = 0x21C_0BB5_0000;

    private const ulong SecondBuffer = 0x21C_0BB6_0000;

    private const ulong Deep = 0x20E_F6E5_005C;

    [Fact]
    public void A_Handoff_Reads_The_Batch_The_Consumer_Was_Given()
    {
        var batch = Replay(BatchMemory())!;

        Assert.Equal((Batch, Consumer, 900), (batch.Address, batch.Consumer, batch.Capacity));
        Assert.Equal([0, 2], batch.Selection);
        Assert.Equal([1, 2], batch.Vectors.Select(v => v.VectorId));
    }

    [Fact]
    public void Rows_Are_Read_Up_To_The_Last_Selected_Row()
    {
        var vector = Replay(BatchMemory())!.Vectors[0];

        Assert.Equal([0x600220ul, 0x6011C0, Deep | 1], vector.Slots);
        Assert.Equal("REF-0003146000", Encoding.ASCII.GetString(vector.GetDeepValue(2)!.Data));
    }

    [Fact]
    public void A_Pure_Vector_Holds_One_Slot()
    {
        var vector = Replay(BatchMemory())!.Vectors[1];

        Assert.True(vector.IsPure);
        Assert.Equal([0xE60100ul], vector.Slots);
    }

    [Fact]
    public void A_Batch_Without_A_Header_Is_Unreadable()
    {
        var memory = new FakeTraceReplay().Write(Data, Batch)
                                          .Write(Data + 8, Ids)
                                          .Write(Ids, (ushort)0);

        Assert.Equal(BatchCaptureFlags.Unreadable, Replay(memory)!.Flags);
    }

    [Fact]
    public void A_Selection_Without_A_Vector_Is_Every_Row()
    {
        var memory = BatchMemory().Write(Batch + 0x38, 0ul);

        var batch = Replay(memory)!;

        Assert.Equal([0, 1], batch.Selection);
        Assert.True(batch.Flags.HasFlag(BatchCaptureFlags.ImplicitSelection));
    }

    private static FakeTraceReplay BatchMemory()
    {
        var text = "REF-0003146000"u8;

        return new FakeTraceReplay().Write(Data, Batch)
                                    .Write(Data + 8, Ids)
                                    .Write(Ids, (ushort)2)
                                    .Write(Ids + 2, (ushort)1)
                                    .Write(Ids + 4, (ushort)2)
                                    .Write(Batch + 0x08, Vectors)
                                    .Write(Batch + 0x10, Pure)
                                    .Write(Batch + 0x1C, 2)
                                    .Write(Batch + 0x24, 900)
                                    .Write(Batch + 0x28, 2)
                                    .Write(Batch + 0x38, Selection)
                                    .Write(Selection, (ushort)0)
                                    .Write(Selection + 2, (ushort)2)
                                    .Write(Pure, (byte)0b10)
                                    .Write(Vectors, FirstBuffer)
                                    .Write(Vectors + 0x58, SecondBuffer)
                                    .Write(FirstBuffer, 0x600220ul)
                                    .Write(FirstBuffer + 8, 0x6011C0ul)
                                    .Write(FirstBuffer + 16, Deep | 1)
                                    .Write(Deep - 2, (ushort)text.Length)
                                    .Write(Deep, text)
                                    .Write(SecondBuffer, 0xE60100ul);
    }

    private static BatchCapture? Replay(FakeTraceReplay memory)
    {
        var hook = new BatchHook(new BatchFunctions([Function]));

        memory.Frames = [new TraceHookFrame(1, 0x55_47EF_B400, Iterator, 5), new TraceHookFrame(2, 0x55_47EF_B800, Consumer, 4)];

        hook.OnEntry(Call(), memory);

        hook.OnReturn(Call(), memory);

        return hook.Build().GetBatch(Function, Iterator, 4);
    }

    private static TraceHookCall Call()
        => new()
        {
            Function = Function,
            Instance = Iterator,
            Id = 200,
            CallCount = 260,
            Call = 4,
            Arguments = new[] { Iterator, Data, 0ul, 0ul }
        };
}
