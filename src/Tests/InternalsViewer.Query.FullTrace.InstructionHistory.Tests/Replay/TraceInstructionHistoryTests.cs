using InternalsViewer.Query.FullTrace.InstructionHistory.Replay;
using InternalsViewer.Query.FullTrace.InstructionHistory.Replay.Enums;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Tests.Replay;

[Trait("Category", "Unit")]
public class TraceInstructionHistoryTests
{
    [Fact]
    public void A_Step_Keeps_Its_Position_Program_Counter_And_Depth()
    {
        var builder = new TraceInstructionHistory.Builder();

        builder.Add([Step(0x10, 1, 0x7FF8_0060_0000, 0, 0, 2)], []);

        var step = builder.Build(TraceInstructionOutcome.ReachedEnd).Steps[0];

        Assert.Equal(new TracePosition(0x10, 1), step.Position);
        Assert.Equal(0x7FF8_0060_0000ul, step.ProgramCounter);
        Assert.Equal(2, step.Depth);
    }

    [Fact]
    public void The_Changes_Of_A_Later_Chunk_Follow_The_Earlier_Ones()
    {
        var builder = new TraceInstructionHistory.Builder();

        builder.Add([Step(1, 0, 0x1000, 0, 1, 0)], [Change(TraceRegister.Rax, 5)]);

        builder.Add([Step(1, 1, 0x1004, 0, 1, 0)], [Change(TraceRegister.Rcx, 7)]);

        var history = builder.Build(TraceInstructionOutcome.FrameReturned);

        Assert.Equal(new TraceRegisterChange(TraceRegister.Rcx, 7, 0), history.GetChanges(1)[0]);
        Assert.Equal(TraceInstructionOutcome.FrameReturned, history.Outcome);
    }

    [Fact]
    public void The_Registers_At_A_Step_Apply_Every_Change_Up_To_It()
    {
        var builder = new TraceInstructionHistory.Builder();

        builder.Add([Step(1, 0, 0x1000, 0, 2, 0), Step(1, 1, 0x1004, 2, 1, 0), Step(1, 2, 0x1008, 3, 1, 0)],
                    [
                        Change(TraceRegister.Rax, 1),
                        Change(TraceRegister.Rsp, 0x100),
                        Change(TraceRegister.Rax, 2),
                        Change(TraceRegister.Rsp, 0xF8)
                    ]);

        var history = builder.Build(TraceInstructionOutcome.ReachedEnd);

        var second = history.GetRegisters(1);

        var third = history.GetRegisters(2);

        Assert.Equal(2ul, second.GetValue(TraceRegister.Rax));
        Assert.Equal(0x100ul, second.GetValue(TraceRegister.Rsp));
        Assert.Equal(0xF8ul, third.GetValue(TraceRegister.Rsp));
    }

    [Fact]
    public void Code_Is_Kept_Once_For_Each_Address()
    {
        var builder = new TraceInstructionHistory.Builder();

        builder.AddCode(0x1004, [1, 2, 3]);

        builder.AddCode(0x1000, [0x90, 0x90]);

        builder.AddCode(0x1004, [0xFF]);

        var history = builder.Build(TraceInstructionOutcome.ReachedEnd);

        Assert.Equal(new byte[] { 1, 2, 3 }, history.GetCode(0x1004).ToArray());
        Assert.Equal(new ulong[] { 0x1000, 0x1004 }, history.GetAddresses());
        Assert.Equal(2, history.AddressCount);
    }

    [Fact]
    public void An_Address_That_Did_Not_Run_Has_No_Code() => Assert.True(TraceInstructionHistory.Empty.GetCode(0x1000).IsEmpty);

    [Fact]
    public void A_Vector_Register_Keeps_Both_Halves()
    {
        var state = new TraceRegisterState();

        state.Apply(new TraceRegisterChange(TraceRegister.Xmm3, 0x11, 0x22));

        Assert.Equal((0x11ul, 0x22ul), state.GetVector(TraceRegister.Xmm3));
    }

    [Fact]
    public void A_Register_The_State_Does_Not_Know_Is_Ignored()
    {
        var state = new TraceRegisterState();

        state.Apply(new TraceRegisterChange((TraceRegister)99, 1, 0));

        Assert.Equal(0ul, state.GetValue(TraceRegister.Rax));
    }

    private static TraceInstructionStep Step(ulong sequence, ulong steps, ulong programCounter, int firstChange, int changeCount, int depth)
        => new(new TracePosition(sequence, steps), programCounter, firstChange, changeCount, depth);

    private static TraceRegisterChange Change(TraceRegister register, ulong value) => new(register, value, 0);
}
