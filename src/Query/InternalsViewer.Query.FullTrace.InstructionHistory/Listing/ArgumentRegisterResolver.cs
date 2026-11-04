using Iced.Intel;
using InternalsViewer.Query.FullTrace.InstructionHistory.Listing.Helpers;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Listing;

internal static class ArgumentRegisterResolver
{
    private const int AllSlots = (1 << RegisterHelper.ArgumentRegisterCount) - 1;

    public static ArgumentRegisterUse[] Resolve(DecodedInstruction[] instructions,
                                                int[] executionOrder,
                                                CancellationToken cancellationToken)
    {
        var candidates = GetCandidates(instructions);

        var read = new int[instructions.Length];

        var written = new int[instructions.Length];

        var executed = new bool[instructions.Length];

        for (var index = 0; index < candidates.Length; index++)
        {
            read[index] = candidates[index].Read;

            written[index] = candidates[index].Written;
        }

        Span<int> pending = stackalloc int[RegisterHelper.ArgumentRegisterCount];

        pending.Fill(-1);

        var overwritten = 0;

        foreach (var index in executionOrder)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (index < 0 || instructions[index].Error != DecoderError.None)
            {
                Discard(pending, written);

                overwritten = AllSlots;

                continue;
            }

            executed[index] = true;

            read[index] &= ~overwritten;

            var writes = candidates[index].Written;

            for (var slot = 0; slot < pending.Length; slot++)
            {
                if ((writes & (1 << slot)) == 0)
                {
                    continue;
                }

                if (pending[slot] >= 0)
                {
                    written[pending[slot]] &= ~(1 << slot);
                }

                pending[slot] = index;
            }

            overwritten |= writes;

            if (IsCall(instructions[index].Instruction))
            {
                pending.Fill(-1);

                overwritten = AllSlots;
            }
        }

        Discard(pending, written);

        var result = new ArgumentRegisterUse[instructions.Length];

        for (var index = 0; index < result.Length; index++)
        {
            if (executed[index])
            {
                result[index] = new ArgumentRegisterUse(read[index], written[index]);
            }
        }

        return result;
    }

    private static ArgumentRegisterUse[] GetCandidates(DecodedInstruction[] instructions)
    {
        var factory = new InstructionInfoFactory();

        var candidates = new ArgumentRegisterUse[instructions.Length];

        for (var index = 0; index < instructions.Length; index++)
        {
            if (instructions[index].Error == DecoderError.None)
            {
                candidates[index] = GetCandidate(factory, instructions[index].Instruction);
            }
        }

        return candidates;
    }

    private static ArgumentRegisterUse GetCandidate(InstructionInfoFactory factory, in Instruction instruction)
    {
        var info = factory.GetInfo(instruction);

        var read = 0;

        var written = 0;

        foreach (var used in info.GetUsedRegisters())
        {
            var slot = RegisterHelper.GetArgumentSlot(used.Register);

            if (slot < 0)
            {
                continue;
            }

            if (RegisterHelper.IsRead(used.Access))
            {
                read |= 1 << slot;
            }

            if (RegisterHelper.IsWrite(used.Access))
            {
                written |= 1 << slot;
            }
        }

        return RegisterHelper.IsZeroingIdiom(instruction) ? new ArgumentRegisterUse(0, written) : new ArgumentRegisterUse(read, written);
    }

    private static void Discard(Span<int> pending, int[] written)
    {
        for (var slot = 0; slot < pending.Length; slot++)
        {
            if (pending[slot] >= 0)
            {
                written[pending[slot]] &= ~(1 << slot);

                pending[slot] = -1;
            }
        }
    }

    private static bool IsCall(in Instruction instruction)
        => instruction.FlowControl is FlowControl.Call or FlowControl.IndirectCall;
}
