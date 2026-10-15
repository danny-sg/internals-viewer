using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.Hooks;

internal static class HookArguments
{
    public const int RegisterCount = 4;

    public const int SlotCount = 16;

    public const int ReturnRegister = 16;

    public static ulong[] LoadStack(ITraceReplay replay, ulong stackPointer, ulong returnAddress)
    {
        var arguments = new ulong[SlotCount - RegisterCount];

        var homeSpace = replay.TryRead(stackPointer, out ulong top) && top == returnAddress ? stackPointer + sizeof(ulong) : stackPointer;

        var read = replay.Read(homeSpace + RegisterCount * sizeof(ulong), MemoryMarshal.AsBytes(arguments.AsSpan())) / sizeof(ulong);

        arguments.AsSpan(read).Clear();

        return arguments;
    }
}
