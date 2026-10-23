using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Stubs.Enums;

namespace InternalsViewer.Query.FullTrace.Stubs;

internal sealed record Stub(ulong Address, StubOperation[] Operations)
{
    public bool TryResolve(Span<ulong> registers, ITraceReplay replay, StubMemory memory, out ulong target)
    {
        Span<ulong> values = stackalloc ulong[registers.Length];

        registers.CopyTo(values);

        target = 0;

        foreach (var operation in Operations)
        {
            var source = operation.IsAbsolute ? 0 : values[operation.Source];

            var address = operation.IsAbsolute ? operation.Address : source + (ulong)operation.Displacement;

            switch (operation.Type)
            {
                case StubOperationType.Load:
                {
                    if (!memory.TryRead(replay, address, out var value))
                    {
                        return false;
                    }

                    values[operation.Destination] = value;

                    break;
                }

                case StubOperationType.Add:
                    values[operation.Destination] = address;
                    break;

                case StubOperationType.Copy:
                    values[operation.Destination] = source;
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
        if (target < TraceAddress.MinAddress)
        {
            return false;
        }

        values.CopyTo(registers);

        return true;
    }
}
