using Iced.Intel;
using InternalsViewer.Query.FullTrace.InstructionHistory.Listing.Helpers;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Listing.Explanations;

internal static class InstructionTemplateLookup
{
    private const string FlagsSentence = " It only sets the flags that the next conditional instruction reads.";

    private const string CopyTemplate = "Copies {1} into {0}.";

    private const string ZeroTemplate = "Sets {0} to zero.";

    private const string TestSelfTemplate = "Checks whether {0} is zero or negative." + FlagsSentence;

    private const string ReserveStackTemplate = "Moves rsp down by {1} bytes, reserving stack space for this function's own values.";

    private const string ReleaseStackTemplate = "Moves rsp back up by {1} bytes, releasing the stack space this function reserved.";

    private const string MultiplyAccumulatorTemplate = "Multiplies {rax} by {0}, putting the result in {rdx} and {rax}.";

    private const string MultiplyTemplate = "Multiplies {0} by {1}.";

    private const string MultiplyIntoTemplate = "Multiplies {1} by {2} and puts the result in {0}.";

    private const string SignExtendTemplate = "Copies {1} into {0}, extending its sign so a negative number stays negative.";

    private const string ShiftLeftTemplate = "Shifts the bits of {0} left by {1} places, multiplying it by 2 for each place.";

    private const string XorTemplate = "Flips every bit of {0} that is set in {1}.";

    private const string FloatingPointCompareTemplate = "Compares the floating-point numbers {0} and {1}." + FlagsSentence;

    private const string PrefetchTemplate = "Asks the processor to start loading {0} into its cache, ready for later use.";

    private const string JumpIfTemplate = "Jumps to {target} {condition}, and otherwise carries on with the next instruction.";

    private const string CopyIfTemplate = "Copies {1} into {0} {condition}, and otherwise leaves {0} unchanged.";

    private const string SetIfTemplate = "Sets {0} to 1 {condition}, and to 0 otherwise.";

    private const string CopyItemTemplate = "Copies one item from the memory at rsi to the memory at rdi, "
                                            + "then moves both addresses on to the next item.";

    private const string CopyItemsTemplate = "Copies rcx {items} from the memory at rsi to the memory at rdi.";

    private const string StoreItemTemplate = "Copies {1} into the memory at rdi, then moves rdi on to the next item.";

    private const string StoreItemsTemplate = "Fills rcx {items} of memory from rdi onwards with copies of {1}.";

    private static Dictionary<Mnemonic, string> Templates { get; } = CreateTemplates();

    private static Dictionary<Mnemonic, string> StringTemplates { get; } = new()
    {
        [Mnemonic.Movsb] = CopyItemTemplate,
        [Mnemonic.Movsw] = CopyItemTemplate,
        [Mnemonic.Movsd] = CopyItemTemplate,
        [Mnemonic.Movsq] = CopyItemTemplate,
        [Mnemonic.Stosb] = StoreItemTemplate,
        [Mnemonic.Stosw] = StoreItemTemplate,
        [Mnemonic.Stosd] = StoreItemTemplate,
        [Mnemonic.Stosq] = StoreItemTemplate
    };

    private static Dictionary<Mnemonic, string> RepeatedStringTemplates { get; } = new()
    {
        [Mnemonic.Movsb] = CopyItemsTemplate,
        [Mnemonic.Movsw] = CopyItemsTemplate,
        [Mnemonic.Movsd] = CopyItemsTemplate,
        [Mnemonic.Movsq] = CopyItemsTemplate,
        [Mnemonic.Stosb] = StoreItemsTemplate,
        [Mnemonic.Stosw] = StoreItemsTemplate,
        [Mnemonic.Stosd] = StoreItemsTemplate,
        [Mnemonic.Stosq] = StoreItemsTemplate
    };

    public static string? GetTemplate(in Instruction instruction)
    {
        if (instruction.IsStringInstruction)
        {
            var strings = instruction.HasRepPrefix ? RepeatedStringTemplates : StringTemplates;

            return strings.TryGetValue(instruction.Mnemonic, out var repeated) ? repeated : null;
        }

        if (RegisterHelper.IsZeroingIdiom(instruction))
        {
            return ZeroTemplate;
        }

        if (instruction.Mnemonic == Mnemonic.Test && RegisterHelper.IsSameRegister(instruction, 0, 1))
        {
            return TestSelfTemplate;
        }

        if (IsStackAdjustment(instruction))
        {
            return instruction.Mnemonic == Mnemonic.Sub ? ReserveStackTemplate : ReleaseStackTemplate;
        }

        if (instruction.Mnemonic == Mnemonic.Imul)
        {
            return instruction.OpCount switch
            {
                1 => MultiplyAccumulatorTemplate,
                2 => MultiplyTemplate,
                _ => MultiplyIntoTemplate
            };
        }

        return Templates.TryGetValue(instruction.Mnemonic, out var template) ? template : null;
    }

    private static bool IsStackAdjustment(in Instruction instruction)
        => instruction.Mnemonic is Mnemonic.Sub or Mnemonic.Add
           && instruction.OpCount == 2
           && instruction.GetOpKind(0) == OpKind.Register
           && instruction.GetOpRegister(0) == Register.RSP
           && instruction.GetOpKind(1) is OpKind.Immediate8to64 or OpKind.Immediate32to64;

    private static Dictionary<Mnemonic, string> CreateTemplates()
    {
        var templates = new Dictionary<Mnemonic, string>
        {
            [Mnemonic.Mov] = CopyTemplate,
            [Mnemonic.Movzx] = "Copies {1} into {0}, filling the extra high bits with zeros.",
            [Mnemonic.Lea] = "Works out the address {address} and puts it in {0}. It only does the arithmetic and does not read memory.",
            [Mnemonic.Xchg] = "Swaps the values of {0} and {1}.",
            [Mnemonic.Push] = "Puts {0} on top of the stack.",
            [Mnemonic.Pop] = "Takes the value on top of the stack and puts it in {0}.",
            [Mnemonic.Add] = "Adds {1} to {0}.",
            [Mnemonic.Adc] = "Adds {1} and the carry flag to {0}.",
            [Mnemonic.Sub] = "Subtracts {1} from {0}.",
            [Mnemonic.Sbb] = "Subtracts {1} and the carry flag from {0}.",
            [Mnemonic.Inc] = "Adds 1 to {0}.",
            [Mnemonic.Dec] = "Subtracts 1 from {0}.",
            [Mnemonic.Neg] = "Changes the sign of {0}, turning a positive number negative and a negative one positive.",
            [Mnemonic.Mul] = "Multiplies {rax} by {0} as unsigned numbers, putting the result in {rdx} and {rax}.",
            [Mnemonic.Div] = "Divides the number held in {rdx} and {rax} by {0} as unsigned numbers. "
                             + "The quotient goes in {rax} and the remainder in {rdx}.",
            [Mnemonic.Idiv] = "Divides the number held in {rdx} and {rax} by {0}. The quotient goes in {rax} and the remainder in {rdx}.",
            [Mnemonic.And] = "Keeps only the bits of {0} that are also set in {1}, clearing the rest.",
            [Mnemonic.Or] = "Sets every bit of {0} that is set in {1}.",
            [Mnemonic.Not] = "Flips every bit of {0}.",
            [Mnemonic.Shr] = "Shifts the bits of {0} right by {1} places, halving it for each place and dropping any remainder.",
            [Mnemonic.Sar] = "Shifts the bits of {0} right by {1} places, halving it for each place and keeping its sign.",
            [Mnemonic.Rol] = "Rotates the bits of {0} left by {1} places, so bits that fall off the top come back in at the bottom.",
            [Mnemonic.Ror] = "Rotates the bits of {0} right by {1} places, so bits that fall off the bottom come back in at the top.",
            [Mnemonic.Bt] = "Copies bit {1} of {0} into the carry flag for the next conditional instruction.",
            [Mnemonic.Bts] = "Copies bit {1} of {0} into the carry flag, then sets that bit to 1.",
            [Mnemonic.Btr] = "Copies bit {1} of {0} into the carry flag, then clears that bit to 0.",
            [Mnemonic.Btc] = "Copies bit {1} of {0} into the carry flag, then flips that bit.",
            [Mnemonic.Bsf] = "Finds the position of the lowest set bit in {1} and puts it in {0}.",
            [Mnemonic.Bsr] = "Finds the position of the highest set bit in {1} and puts it in {0}.",
            [Mnemonic.Tzcnt] = "Counts the zero bits at the bottom of {1} and puts the count in {0}.",
            [Mnemonic.Lzcnt] = "Counts the zero bits at the top of {1} and puts the count in {0}.",
            [Mnemonic.Popcnt] = "Counts the bits set in {1} and puts the count in {0}.",
            [Mnemonic.Bswap] = "Reverses the order of the bytes in {0}.",
            [Mnemonic.Cmp] = "Compares {0} with {1}." + FlagsSentence,
            [Mnemonic.Test] = "Checks which bits {0} and {1} have in common." + FlagsSentence,
            [Mnemonic.Cmpxchg] = "Compares {rax} with {0} and, if they are equal, replaces {0} with {1}, otherwise loads {0} into {rax}.",
            [Mnemonic.Xadd] = "Adds {1} to {0} and puts the old value of {0} in {1}.",
            [Mnemonic.Cdqe] = "Widens eax into rax, keeping its sign.",
            [Mnemonic.Cwde] = "Widens ax into eax, keeping its sign.",
            [Mnemonic.Cdq] = "Fills edx with the sign of eax, ready for a division.",
            [Mnemonic.Cqo] = "Fills rdx with the sign of rax, ready for a division.",
            [Mnemonic.Jmp] = "Jumps to {target}.",
            [Mnemonic.Call] = "Calls the function at {target}, passing the first four arguments in rcx, rdx, r8 and r9. "
                              + "Its own instructions are not listed, and the next line runs once it returns with any result in rax.",
            [Mnemonic.Ret] = "Returns to the function that called this one, with any result in rax.",
            [Mnemonic.Leave] = "Puts rsp back to rbp and takes the saved rbp off the stack, undoing this function's stack frame.",
            [Mnemonic.Nop] = "Does nothing. The compiler adds it as padding to line up the next instruction.",
            [Mnemonic.Int3] = "Stops in a debugger if one is attached. The compiler also uses it as padding between functions.",
            [Mnemonic.Pause] = "Tells the processor that this is a loop waiting on another thread, so it can save power while it waits.",
            [Mnemonic.Lfence] = "Waits until every earlier memory read has finished before carrying on.",
            [Mnemonic.Sfence] = "Waits until every earlier memory write has finished before carrying on.",
            [Mnemonic.Mfence] = "Waits until every earlier memory read and write has finished before carrying on.",
            [Mnemonic.Rdtsc] = "Reads the processor's time-stamp counter, which counts clock ticks, into edx and eax."
        };

        Add(templates, SignExtendTemplate, Mnemonic.Movsx, Mnemonic.Movsxd);

        Add(templates, ShiftLeftTemplate, Mnemonic.Shl, Mnemonic.Sal);

        Add(templates,
            CopyTemplate,
            Mnemonic.Movaps,
            Mnemonic.Movups,
            Mnemonic.Movapd,
            Mnemonic.Movupd,
            Mnemonic.Movdqa,
            Mnemonic.Movdqu,
            Mnemonic.Movd,
            Mnemonic.Movq,
            Mnemonic.Movss,
            Mnemonic.Movsd,
            Mnemonic.Vmovaps,
            Mnemonic.Vmovups,
            Mnemonic.Vmovdqa,
            Mnemonic.Vmovdqu);

        Add(templates, XorTemplate, Mnemonic.Xor, Mnemonic.Pxor, Mnemonic.Xorps, Mnemonic.Xorpd);

        Add(templates, FloatingPointCompareTemplate, Mnemonic.Ucomiss, Mnemonic.Ucomisd, Mnemonic.Comiss, Mnemonic.Comisd);

        Add(templates,
            PrefetchTemplate,
            Mnemonic.Prefetchnta,
            Mnemonic.Prefetcht0,
            Mnemonic.Prefetcht1,
            Mnemonic.Prefetcht2,
            Mnemonic.Prefetchw);

        Add(templates, "Adds {1} to {0} as floating-point numbers.", Mnemonic.Addss, Mnemonic.Addsd);

        Add(templates, "Subtracts {1} from {0} as floating-point numbers.", Mnemonic.Subss, Mnemonic.Subsd);

        Add(templates, "Multiplies {0} by {1} as floating-point numbers.", Mnemonic.Mulss, Mnemonic.Mulsd);

        Add(templates, "Divides {0} by {1} as floating-point numbers.", Mnemonic.Divss, Mnemonic.Divsd);

        Add(templates, "Puts the square root of {1} in {0}.", Mnemonic.Sqrtss, Mnemonic.Sqrtsd);

        Add(templates, "Converts the whole number {1} to a floating-point number in {0}.", Mnemonic.Cvtsi2ss, Mnemonic.Cvtsi2sd);

        Add(templates,
            "Converts the floating-point number {1} to a whole number in {0}, dropping any fraction.",
            Mnemonic.Cvttss2si,
            Mnemonic.Cvttsd2si);

        Add(templates,
            JumpIfTemplate,
            Mnemonic.Jo,
            Mnemonic.Jno,
            Mnemonic.Jb,
            Mnemonic.Jae,
            Mnemonic.Je,
            Mnemonic.Jne,
            Mnemonic.Jbe,
            Mnemonic.Ja,
            Mnemonic.Js,
            Mnemonic.Jns,
            Mnemonic.Jp,
            Mnemonic.Jnp,
            Mnemonic.Jl,
            Mnemonic.Jge,
            Mnemonic.Jle,
            Mnemonic.Jg);

        Add(templates,
            CopyIfTemplate,
            Mnemonic.Cmovo,
            Mnemonic.Cmovno,
            Mnemonic.Cmovb,
            Mnemonic.Cmovae,
            Mnemonic.Cmove,
            Mnemonic.Cmovne,
            Mnemonic.Cmovbe,
            Mnemonic.Cmova,
            Mnemonic.Cmovs,
            Mnemonic.Cmovns,
            Mnemonic.Cmovp,
            Mnemonic.Cmovnp,
            Mnemonic.Cmovl,
            Mnemonic.Cmovge,
            Mnemonic.Cmovle,
            Mnemonic.Cmovg);

        Add(templates,
            SetIfTemplate,
            Mnemonic.Seto,
            Mnemonic.Setno,
            Mnemonic.Setb,
            Mnemonic.Setae,
            Mnemonic.Sete,
            Mnemonic.Setne,
            Mnemonic.Setbe,
            Mnemonic.Seta,
            Mnemonic.Sets,
            Mnemonic.Setns,
            Mnemonic.Setp,
            Mnemonic.Setnp,
            Mnemonic.Setl,
            Mnemonic.Setge,
            Mnemonic.Setle,
            Mnemonic.Setg);

        return templates;
    }

    private static void Add(Dictionary<Mnemonic, string> templates, string template, params ReadOnlySpan<Mnemonic> mnemonics)
    {
        foreach (var mnemonic in mnemonics)
        {
            templates[mnemonic] = template;
        }
    }
}
