using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Stubs.Enums;

namespace InternalsViewer.Query.FullTrace.Stubs;

/// <summary>
/// Stub defined by address and decoded operations
/// </summary>
internal sealed record Stub(ulong Address, StubOperation[] Operations)
{
    /// <summary>
    /// Resolves a stub target by virtually executing the stub with registers/memory
    /// </summary>
    /// <remarks>
    /// A Stub is the definition of a function/operations that have previously been identified as a Stub during the decode process.
    ///
    /// To resolve to an address the operations have to be executed to determine the final target address. This needs to be done at replay
    /// in the context of the provided current register values and memory state.
    ///
    /// Operations are limited to a very small set of instructions - Load, Add, Copy, and Jump (Memory/Register/Direct).
    ///
    /// The result of any jump operation is the resolved target address, which is returned. Memory jumps are read from the provided
    /// StubMemory, which is a wrapper around the replay memory.
    ///
    /// If a stub is resolved successfully the result of any changes to the registers during the execution of the stub are written back to
    /// the provided registers span. If the stub cannot be resolved the registers are left unchanged.
    /// </remarks>
    public bool TryResolve(Span<ulong> registers, ITraceReplay replay, StubMemory memory, out ulong target)
    {
        Span<ulong> values = stackalloc ulong[registers.Length];

        registers.CopyTo(values);

        target = 0;

        foreach (var operation in Operations)
        {
            var source = operation.IsAbsolute ? 0 : values[operation.SourceRegister];

            var address = operation.IsAbsolute ? operation.Address : source + (ulong)operation.Displacement;

            switch (operation.Type)
            {
                case StubOperationType.Load:
                {
                    if (!memory.TryRead(replay, address, out var value))
                    {
                        return false;
                    }

                    values[operation.DestinationRegister] = value;

                    break;
                }

                case StubOperationType.Add:
                    values[operation.DestinationRegister] = address;
                    break;

                case StubOperationType.Copy:
                    values[operation.DestinationRegister] = source;
                    break;

                case StubOperationType.JumpMemory:
                    return memory.TryRead(replay, address, out target) && Accept(values, registers, target);

                case StubOperationType.JumpRegister:
                    target = source;

                    return Accept(values, registers, target);

                case StubOperationType.JumpDirect:
                    target = operation.Address;

                    return Accept(values, registers, target);
            }
        }

        return false;
    }

    private static bool Accept(ReadOnlySpan<ulong> values, Span<ulong> registers, ulong target)
    {
        if (target < TraceAddress.MinimumAddress)
        {
            return false;
        }

        values.CopyTo(registers);

        return true;
    }
}
