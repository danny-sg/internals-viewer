using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.InstructionHistory.Listing;
using InternalsViewer.Query.FullTrace.InstructionHistory.Replay;
using InternalsViewer.Query.FullTrace.InstructionHistory.Replay.Enums;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Tests.Listing;

[Trait("Category", "Unit")]
public class InstructionListingBuilderTests
{
    private static InstructionRequest Request { get; } = new(new CallstackFrame(), 1, new TracePosition(10, 0), new TracePosition(20, 0));

    private static Dictionary<ulong, string> Loop { get; } = new()
    {
        [0x1000] = "4889C8",
        [0x1003] = "83F805",
        [0x1006] = "75F8",
        [0x1008] = "B901000000",
        [0x100D] = "E8EE0F0000",
        [0x1012] = "C3"
    };

    private static ulong[] LoopOrder { get; } = [0x1000, 0x1003, 0x1006, 0x1000, 0x1003, 0x1006, 0x1008, 0x100D, 0x1012];

    [Fact]
    public async Task Each_Address_That_Ran_Is_One_Line_In_Address_Order()
    {
        var listing = await BuildAsync(Loop, LoopOrder);

        Assert.Equal(new ulong[] { 0x1000, 0x1003, 0x1006, 0x1008, 0x100D, 0x1012 }, listing.Lines.Select(l => l.Address));
    }

    [Fact]
    public async Task The_Text_Is_The_Execution_Count_And_The_Intel_Syntax()
    {
        var listing = await BuildAsync(Loop, LoopOrder);

        var expected = new[]
        {
            "2  mov     rax, rcx",
            "2  cmp     eax, 5",
            "2  jne     0x1000",
            "1  mov     ecx, 1",
            "1  call    0x2000",
            "1  ret"
        };

        Assert.Equal(expected, listing.Lines.Select(l => l.Text));
    }

    [Fact]
    public async Task The_Execution_Counts_Line_Up_On_The_Right()
    {
        var order = Enumerable.Repeat(0x1000ul, 1_200).Append(0x1001ul).ToArray();

        var listing = await BuildAsync(new Dictionary<ulong, string> { [0x1000] = "90", [0x1001] = "C3" }, order);

        Assert.Equal(1_200.ToString("N0") + "  nop", listing.Lines[0].Text);
        Assert.Equal("1".PadLeft(1_200.ToString("N0").Length) + "  ret", listing.Lines[1].Text);
    }

    [Fact]
    public async Task A_Register_Read_Before_The_Function_Writes_It_Is_Named_As_An_Argument()
    {
        var listing = await BuildAsync(Loop, LoopOrder);

        Assert.Equal("Copies rcx (the first argument) into rax.", GetExplanation(listing, 0x1000));
    }

    [Fact]
    public async Task A_Comparison_Only_Sets_Flags_For_The_Next_Conditional_Instruction()
    {
        var listing = await BuildAsync(Loop, LoopOrder);

        Assert.Equal("Compares eax with 5. It only sets the flags that the next conditional instruction reads.",
                     GetExplanation(listing, 0x1003));
    }

    [Fact]
    public async Task A_Conditional_Jump_Names_Its_Target_And_Its_Condition()
    {
        var listing = await BuildAsync(Loop, LoopOrder);

        Assert.Equal("Jumps to 0x1000 if the last comparison was not equal, and otherwise carries on with the next instruction.",
                     GetExplanation(listing, 0x1006));
    }

    [Fact]
    public async Task A_Register_Written_For_The_Next_Call_Is_Named_As_Its_Argument()
    {
        var listing = await BuildAsync(Loop, LoopOrder);

        Assert.Equal("Copies 1 into ecx (the first argument of the next call).", GetExplanation(listing, 0x1008));
    }

    [Fact]
    public async Task A_Call_Names_Its_Target_And_The_Argument_Registers()
    {
        var listing = await BuildAsync(Loop, LoopOrder);

        Assert.Equal("Calls the function at 0x2000, passing the first four arguments in rcx, rdx, r8 and r9. "
                     + "Its own instructions are not listed, and the next line runs once it returns with any result in rax.",
                     GetExplanation(listing, 0x100D));
    }

    [Fact]
    public async Task A_Return_Goes_Back_To_The_Caller()
    {
        var listing = await BuildAsync(Loop, LoopOrder);

        Assert.Equal("Returns to the function that called this one, with any result in rax.", GetExplanation(listing, 0x1012));
    }

    [Fact]
    public async Task An_Argument_Register_Overwritten_Before_The_Call_Is_Not_Named_As_An_Argument()
    {
        var code = new Dictionary<ulong, string>
        {
            [0x4000] = "B901000000",
            [0x4005] = "4889C8",
            [0x4008] = "B902000000",
            [0x400D] = "E8EE0F0000",
            [0x4012] = "C3"
        };

        var listing = await BuildAsync(code, [0x4000, 0x4005, 0x4008, 0x400D, 0x4012]);

        Assert.Equal("Copies 1 into ecx.", GetExplanation(listing, 0x4000));
        Assert.Equal("Copies rcx into rax.", GetExplanation(listing, 0x4005));
        Assert.Equal("Copies 2 into ecx (the first argument of the next call).", GetExplanation(listing, 0x4008));
    }

    [Fact]
    public async Task A_Mnemonic_Without_A_Template_Names_The_Registers_It_Reads_And_Writes()
    {
        var listing = await BuildAsync(new Dictionary<ulong, string> { [0x1000] = "0FA2" }, [0x1000]);

        Assert.Equal("1  cpuid", listing.Lines[0].Text);
        Assert.Equal("Runs the cpuid instruction, which reads eax and ecx, and writes rax, rcx, rdx and rbx.",
                     listing.Lines[0].Explanation);
    }

    [Fact]
    public async Task Bytes_That_Are_Not_An_Instruction_Are_Shown_As_Data()
    {
        var listing = await BuildAsync(new Dictionary<ulong, string> { [0x1000] = "0690909090909090909090909090" }, [0x1000]);

        Assert.Equal("1  db 0x06", listing.Lines[0].Text);
        Assert.Equal("These bytes are not an x64 instruction the decoder recognises.", listing.Lines[0].Explanation);
    }

    [Fact]
    public async Task A_Code_Read_That_Ends_With_The_Instruction_Decodes()
    {
        var listing = await BuildAsync(new Dictionary<ulong, string> { [0x1000] = "488B4110" }, [0x1000]);

        Assert.Equal("1  mov     rax, [rcx+0x10]", listing.Lines[0].Text);
        Assert.Equal("Copies the 8 bytes at rcx+0x10 (rcx is the first argument) into rax.", listing.Lines[0].Explanation);
    }

    [Fact]
    public async Task A_Code_Read_That_Stops_Inside_The_Instruction_Is_Shown_As_Data()
    {
        var listing = await BuildAsync(new Dictionary<ulong, string> { [0x1000] = "488B" }, [0x1000]);

        Assert.Equal("1  db 0x48, 0x8B", listing.Lines[0].Text);
        Assert.Equal("The recording holds only part of this instruction, so it cannot be decoded.", listing.Lines[0].Explanation);
    }

    [Fact]
    public async Task Only_The_First_Instruction_Of_A_Code_Read_Is_Decoded()
    {
        var listing = await BuildAsync(new Dictionary<ulong, string> { [0x1000] = "90C3CCCCCCCCCCCCCCCCCCCCCCCCCC" }, [0x1000]);

        Assert.Equal("1  nop", Assert.Single(listing.Lines).Text);
    }

    [Theory]
    [InlineData("33C0", "Sets eax to zero.")]
    [InlineData("4885C0", "Checks whether rax is zero or negative. It only sets the flags that the next conditional instruction reads.")]
    [InlineData("4883EC28", "Moves rsp down by 0x28 bytes, reserving stack space for this function's own values.")]
    [InlineData("4883C428", "Moves rsp back up by 0x28 bytes, releasing the stack space this function reserved.")]
    [InlineData("488D4C2420", "Works out the address rsp+0x20 and puts it in rcx. It only does the arithmetic and does not read memory.")]
    [InlineData("0F95C0", "Sets al to 1 if the last comparison was not equal, and to 0 otherwise.")]
    [InlineData("F3AA", "Fills rcx bytes of memory from rdi onwards with copies of al.")]
    [InlineData("53", "Puts rbx on top of the stack.")]
    [InlineData("65488B042558000000", "Copies the 8 bytes at gs:0x58 (data that belongs to the current thread) into rax.")]
    [InlineData("F00FC101",
                "Adds eax to the 4 bytes at rcx (rcx is the first argument) and puts the old value of the 4 bytes at rcx in eax. "
                + "The lock prefix makes the whole change in one step, so other threads never see it half done.")]
    public async Task An_Instruction_Is_Explained_In_Plain_English(string code, string explanation)
    {
        var listing = await BuildAsync(new Dictionary<ulong, string> { [0x1000] = code }, [0x1000]);

        Assert.Equal(explanation, listing.Lines[0].Explanation);
    }

    [Fact]
    public async Task An_Empty_History_Has_No_Lines()
    {
        var builder = new InstructionListingBuilder();

        var listing = await builder.BuildAsync(Request, TraceInstructionHistory.Empty, CancellationToken.None);

        Assert.Empty(listing.Lines);
    }

    [Fact]
    public async Task A_Cancelled_Build_Stops()
    {
        using var cancellation = new CancellationTokenSource();

        await cancellation.CancelAsync();

        var history = CreateHistory(Loop, LoopOrder);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new InstructionListingBuilder().BuildAsync(Request, history, cancellation.Token));
    }

    private static Task<InstructionListing> BuildAsync(Dictionary<ulong, string> code, ulong[] order)
        => new InstructionListingBuilder().BuildAsync(Request, CreateHistory(code, order), CancellationToken.None);

    private static TraceInstructionHistory CreateHistory(Dictionary<ulong, string> code, ulong[] order)
    {
        var builder = new TraceInstructionHistory.Builder();

        var steps = new TraceInstructionStep[order.Length];

        for (var index = 0; index < order.Length; index++)
        {
            steps[index] = new TraceInstructionStep(new TracePosition(1, (ulong)index), order[index], 0, 0, 0);
        }

        builder.Add(steps, []);

        foreach (var (address, bytes) in code)
        {
            builder.AddCode(address, Convert.FromHexString(bytes));
        }

        return builder.Build(TraceInstructionOutcome.FrameReturned);
    }

    private static string? GetExplanation(InstructionListing listing, ulong address)
        => listing.Lines.Single(l => l.Address == address).Explanation;
}
