using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Hooks;

internal static class TraceHookArguments
{
    public static ulong GetStackArguments(ITraceReplay replay, in TraceHookCall call)
    {
        var hasReturnAddress = replay.TryRead(call.StackPointer, out ulong top) && top == call.ReturnAddress;

        return call.StackPointer + (ulong)(hasReturnAddress ? ArgumentLayout.StackArgumentOffset : ArgumentLayout.HomeSpaceSize);
    }

    public static ulong GetArgument(ITraceReplay replay, in TraceHookCall call, int slot)
    {
        if (slot < ArgumentLayout.RegisterCount)
        {
            return call.Arguments[slot];
        }

        var address = GetStackArguments(replay, call) + (ulong)((slot - ArgumentLayout.RegisterCount) * sizeof(ulong));

        return replay.TryRead(address, out ulong value) ? value : 0;
    }
}
